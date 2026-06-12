# Category Blueprints — AI-Assisted List Sorting (Design)

**Status:** Approved design, ready for implementation planning.
**Date:** 2026-06-12
**Prerequisite (shipped):** Aisle-level `ProductCategory` taxonomy — 23 supermarket aisles + 2 sentinels (`docs/superpowers/specs/2026-06-12-aisle-product-taxonomy-design.md`, merged to `stage`).

## Goal

Let a household define reusable **category blueprints** — ordered subsets of supermarket aisles matching a store's walk-order — and apply one to a list on demand to reorder the list's unchecked items into that order. The sort is deterministic: it reads each item's already-classified `ProductCategory` and reorders by the blueprint's category ranks. **No LLM calls at sort time** — classification already runs in the background.

## Why (use case)

List items are ordered only by manual drag today. For grocery shopping the useful order is "the order you walk your store" — and it's the same trip after trip, per store. A household shops more than one store, each with its own layout. So:

> Before store A, apply the "Store A" blueprint → unchecked items reorder into store A's aisle walk-order. Shop and check items off. At store B, apply the "Store B" blueprint → whatever is *still unchecked* reorders into store B's layout. The checked-off items stay put.

Apply is a one-shot, explicit action. New items added later append per normal logic; the user re-runs a blueprint when they want to re-sort. There is **no persisted list↔blueprint binding** — applying is a bulk reorder, not a saved mode.

## Rejected alternative (recorded)

Free-text prompt at sort time ("sort like my local Aldi" → LLM) is **rejected for v1**: nondeterministic (order shuffles between identical sorts), unexplainable, adds per-sort latency + token cost, and opens a prompt-injection surface on user item text. A structured blueprint keeps the AI's job as *classification* (shipped) and makes the *sort* deterministic, cheap, and reviewable. A prompt could later earn a place at **configuration** time (LLM proposes a category order from a typed store description, Admin reviews/edits, saved as a static blueprint) — parked, not in v1.

---

## Data model

A new household-scoped aggregate, sitting **alongside** `HouseholdSettings` (not inside it — a blueprint library is a collection, not a singleton settings row).

### `SortBlueprint`
| Column | Notes |
| --- | --- |
| `Id` | identity PK |
| `HouseholdId` | FK → `Household` |
| `Name` | `HasMaxLength` matched to the existing `List.Title` width (no new width constant — avoid unnecessary migration churn) |
| `IsActive` | soft-delete; central `ApplicationDbContext` pattern, filtered per-slice |
| `CreatedAt` / `UpdatedAt` | auto-stamped in `SaveChangesAsync` |

Navigation: `Household`, and `Categories` (the ordered child set).

### `SortBlueprintCategory` (child table)
| Column | Notes |
| --- | --- |
| `BlueprintId` | FK → `SortBlueprint` |
| `Category` | `ProductCategory` enum (stored int, EF default) |
| `OrderIndex` | 0-based position = walk-order rank |

Key: composite `(BlueprintId, Category)` — a category appears at most once per blueprint. Rows are **replaced wholesale** on each edit (the set is tiny, ≤23, and always read/written as a unit). The "available aisles" pool (aisles not in the blueprint) is the complement, computed client-side and **never persisted**.

> Chosen over an ordered `integer[]` column on the blueprint row: the child table is conventional EF, needs no Npgsql array mapping the codebase doesn't otherwise use, and matches how the rest of the domain models relations.

This is the first part of the feature with **real schema + API changes** — an EF migration **and** `npm run api` regen are both in scope (the taxonomy part needed neither).

---

## Domain rules & methods

All `FluentResults`-returning, on the `SortBlueprint` aggregate unless noted. Role gate is `callerRole.CanManageSettings()` (= `>= Admin`, Owner/Admin) — the same gate `UpdateHouseholdSettings` already uses.

- **`SortBlueprint.Create(householdId, name, IReadOnlyList<ProductCategory> orderedCategories, HouseholdRole callerRole)`**
  - Gate: `CanManageSettings()` → else `AccessDeniedError`.
  - Validate: `name` non-empty and ≤ max length; `orderedCategories` **distinct**; every entry is a **real aisle** (rejects the `Unknown` and `Other` sentinels — they can never be ranked, they always sink).
  - Materializes `SortBlueprintCategory` rows with `OrderIndex` = list position.
- **`blueprint.Update(name, orderedCategories, callerRole)`** — same gates/validation; replaces name + the full ordered child set.
- **`blueprint.SoftDelete(callerRole)`** — Admin/Owner; sets `IsActive = false`.
- **`SortBlueprint.CreateDefault(householdId)`** — seed factory, no role gate (system-created). Canonical full walk-order over all 23 aisles:
  `Produce → Bakery → DeliAndColdCuts → Meat → Fish → DairyAndEggs → Cheese → Frozen → Cereal → Pantry → CannedGoods → Sauces → OilsAndVinegar → Spices → Spreads → Snacks → Sweets → Beverages → Alcohol → HouseholdAndCleaning → HealthAndBeauty → Baby → Pet`.
  Named "Supermarket (default)" (display name via i18n on the client; the stored `Name` is a plain seed string the user can rename).

### Apply lives on `List`
- **`List.ApplyOrder(IReadOnlyList<int> orderedUncheckedItemIds)`** — re-mints the fractional-index `Rank` for the **unchecked** section so items land in exactly the given order. Invariants: every id belongs to this list's unchecked section, and the set matches the current unchecked items (no adds/drops). The **checked** section is untouched. This is pure (no `Product` knowledge) and reusable; the handler computes the order and hands it in.

---

## Apply algorithm (handler)

The `ApplyBlueprint` slice computes the ordering — it needs `Product` data, which the `List` aggregate deliberately does not hold — then calls the pure `List.ApplyOrder`.

1. Load the list's **unchecked** items, the blueprint + its ordered categories, and the household's `Product` rows for those items' normalized names (inline EF read).
2. Per item: `Text → ProductName.Normalize() → Product.EffectiveCategory`, defaulting to `Unknown` when there's no product row yet (not classified).
3. Sort key per item:
   - primary = the blueprint's `OrderIndex` for that category, or **+∞** if the category is not in this blueprint, or is `Unknown`/`Other`, or the item is unclassified — i.e. the **uncategorized bucket**;
   - tiebreak = the item's **current `Rank`** (stable — items in the same aisle, and the whole uncategorized bucket, keep their existing relative order).
4. `list.ApplyOrder(orderedIds)` re-mints ranks; persist with the existing `RankRetry.SaveWithRetryAsync` collision guard.

Uncategorized items sink to the bottom of the unchecked section. Re-running after classification catches up re-sorts them cleanly.

---

## Backend slices (vertical slices, no controllers)

| Method + route | Auth | Purpose |
| --- | --- | --- |
| `GET /households/{householdId}/blueprints` | any active member | List blueprints (id, name, ordered categories) |
| `POST /households/{householdId}/blueprints` | Admin/Owner | Create |
| `PUT /households/{householdId}/blueprints/{id}` | Admin/Owner | Rename + reorder/curate |
| `DELETE /households/{householdId}/blueprints/{id}` | Admin/Owner | Soft-delete |
| `POST /households/{householdId}/lists/{listId}/apply-blueprint` body `{ blueprintId }` | any active member | Reorder the list's unchecked items; returns the reordered items |

Apply is **any active member** because it is just a reorder of a shared list, which members can already do by dragging. Managing the library (create/edit/delete) is Admin/Owner, matching `CanManageSettings`. Error mapping follows the standard slice dispatch (`EntityNotFoundError` → 404, `AccessDeniedError` → 403, generic `Error` w/ `Property` → `ValidationProblem`).

**Default-blueprint seeding:** lazy. `GET /blueprints` seeds the "Supermarket (default)" blueprint for the household if it has none (so existing households get it on first read too), then returns the library. Idempotent and cheap.

---

## Frontend

### Blueprint editor (one blueprint) — the two-block list metaphor
Reuses the visual shape of a list (a variant of `SortableList`):
- **Top block — "In this blueprint"**: the curated aisles, **drag-to-reorder** = the store walk-order. Each row has a remove control that drops it into the bottom block.
- **Bottom block — "Available aisles"**: the aisles not in the blueprint (all 23 minus the included ones — computed client-side). Each has an add control that appends it to the end of the top block.

The included/ordered block is the draggable one — the inverse of a real list, where the *unchecked* block is the draggable one — so this is a small variant of `SortableList`, not a verbatim reuse. Membership toggling = the checkbox metaphor; reordering the top block = the walk-order. Saving sends the full ordered top block to `PUT`/`POST`.

### Blueprint library (manager)
A "Sort blueprints" section in the household-settings area → a dedicated manage route (the editor wants room). Shows the household's blueprints (name + aisle count) with **create / rename / duplicate / delete**. Members can *view* the library; create/edit/delete is Admin/Owner. "Duplicate" is a client convenience that POSTs a copy.

### List page — apply
A **"Sort by category" menu item** (in the list's overflow menu) → opens a small **popup** (dialog/popover) listing the household's blueprints → selecting one fires `useApplyBlueprint`. **No optimistic reorder** — a quick loading state in the popup, then `onSuccess` invalidates the list-items query and the authoritative order refetches. The checked section is untouched. *(Deferred polish: remember the last-used blueprint per list in localStorage so re-running at the same store is one tap.)*

### Hooks & i18n
One-hook-per-file, canonical shape (spread generated `*Options` / `*Mutation` / `get*QueryKey`): `useSortBlueprints` (query), `useCreateSortBlueprint` / `useUpdateSortBlueprint` / `useDeleteSortBlueprint` / `useApplyBlueprint` (mutations). Aisle display names come from new i18n keys (en + de), one per `ProductCategory`. Tests assert on testids / `data-*`, never translated text.

---

## Verification scope

- EF migration (`SortBlueprint` + `SortBlueprintCategory`); applied automatically at startup.
- `npm run api` regen (new DTOs + endpoints → regenerated TS client).
- **Domain unit tests:** `Create`/`Update`/`SoftDelete` role gating + validation (non-empty name, distinct categories, sentinel rejection); `CreateDefault` content; `List.ApplyOrder` (re-mints unchecked ranks, leaves checked untouched, rejects foreign/missing ids).
- **Handler tests:** category resolution (text → product → category), uncategorized-to-bottom, stable within-aisle tiebreak, member-vs-admin auth on each slice.
- **Integration (Reqnroll + Playwright):** create a blueprint, apply it to a list, assert the resulting item order by testid; the stub classifier already maps `milk→DairyAndEggs`, `soap→HouseholdAndCleaning`, etc.
- Final gate: full-sln `dotnet test` + `docker build`.

## Out of scope / follow-up
- Free-prompt blueprint authoring (LLM-proposed order, human-reviewed) — parked.
- Persisted list↔blueprint binding / auto-re-apply on new items — v1 is explicit one-shot.
- Per-list last-used-blueprint memory — optional client polish, deferred.
- Inventory sorting — this feature is list-only.
