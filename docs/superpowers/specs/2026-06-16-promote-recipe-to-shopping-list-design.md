# Promote Recipe to Shopping List — Design

**Date:** 2026-06-16
**Status:** Approved (brainstorming) — ready for implementation plan
**Branch:** `feat/recipe-copy-to-list`

## Summary

Let a user turn a recipe's ingredients into rows on a shopping **List** with one action:
open a recipe → "Add to shopping list" → a sheet shows the ingredients with editable
quantities and per-item checkboxes → pick a target list → copy. This is the headline
reason recipes exist as ingredient lists ("cooking this tonight → add everything to
Groceries").

The MVP recipe-item shape already mirrors `ListItem` (Text 500, Comment 500, shared
`Quantity` VO), so the copy is cheap.

## Decisions

| Question | Decision |
|---|---|
| Target | **Existing list only** — pick from the household's lists (mirrors the inventory picker in list→inventory promote). |
| Servings scaling | **Scaled (WYSIWYG)** — copy quantities as displayed for the current servings multiplier; computed client-side, editable in the sheet. |
| Duplicates | **Append as-is** — no name-matching, no quantity merge. Predictable, no unit math. |
| Backend shape | **Approach A — dedicated bulk slice** (one request, one transaction), mirroring the existing `PromoteListItems` atomic-batch precedent. |

### Two findings that shaped the design

1. **No "resolution" tracking on the recipe side.** Unlike list→inventory (which stamps
   `PromotionResolvedAt` so an item can't be promoted twice), a recipe is reusable — you
   cook it many times. This is a **stateless one-shot copy**: no pending-batch, no resolved
   flag, nothing mutated on the recipe.

2. **Classification does not fire on list-item create.** The IDEAS note said classification
   (aisle/expiry) fires on the create pipeline; in reality it fires when an item is
   **checked off** (`ToggleItemStatus` does the product-catalog lookup). So copied items
   need no classification at copy time — it happens naturally later while shopping. The copy
   is therefore a plain list-item create.

## Backend — the bulk slice

**New slice:** `Application/Frigorino.Features/Recipes/CopyToList/CopyRecipeToList.cs`,
registered on the recipes `MapGroup` via `MapCopyRecipeToList()`.

**Route:** `POST /api/household/{householdId}/recipes/{recipeId}/copy-to-list`

**DTOs** (entries reference source items by id; client sends only the editable bits —
mirrors `PromoteEntry`):

```csharp
record CopyRecipeToListRequest(int TargetListId, List<CopyEntry> Items);
record CopyEntry(int RecipeItemId, QuantityDto? Quantity);  // Quantity = scaled/edited value; null = text-only
record CopyRecipeToListResponse(int CopiedCount);
```

The client sends `RecipeItemId` + final `Quantity` only. The handler reads `Text` /
`Comment` from the recipe item **server-side** (not client-trusted).

**Handler flow:**

1. Load the recipe with its active items, scoped to the household; member read-access.
   Not found → 404.
2. Load the target list scoped to the household; **same authorization as creating a list
   item**. Not found → 404, denied → 403.
3. For each entry: find the active recipe item by id; **silently skip** ids not in the
   recipe (idempotent, like the promote skip-already-resolved guard). Call the **same
   aggregate method `CreateItem` uses** (`list.AddItem(text, comment, quantity)`). Items
   land **unchecked**, `Type=Text`, appended to the unchecked rank. Comment carries over.
4. One `SaveChangesAsync`. Return `{ copiedCount }` — reflects what actually landed.

Error dispatch follows the slice convention (`EntityNotFoundError` → 404,
`AccessDeniedError` → 403, validation `Error` → `ValidationProblem`).

**Deliberately NOT done:** no async quantity extraction (explicit quantities skip
`ItemTextRouter`); no classification (fires on later check-off); no media copy; no
recipe-side mutation; no quantity merge/dedup; recipe sections flattened (a List is flat).

## Frontend

**New feature folder:** `src/features/recipes/copyToList/`

**Entry point.** Add an item to `RecipeActionsMenu.tsx` (currently Delete-only): "Add to
shopping list". Keeps the header clean. (Open swap: a visible button by the servings
control for discoverability — decide during implementation.)

**`CopyToListSheet.tsx`** — a bottom `Drawer`, structurally a trimmed `PromoteReviewSheet`:

- **List picker** — `TextField select` of the household's lists (existing `getListsOptions`
  query), default newest-first like the inventory picker. If the household has **no lists**,
  show an empty-state ("Create a list first") with a link to list creation instead of the
  picker.
- **Select-all** master checkbox.
- **Per-row:** checkbox + ingredient text (read-only) + editable `QuantityDraftFields`,
  pre-filled with the already-scaled quantity. **No** expiry picker (inventory-only).
- **Action button:** "Add N items to {List}", disabled when nothing selected, no list
  chosen, or a selected row has an *invalid* quantity (value/unit mismatch). An **empty**
  quantity is valid — recipe items are legitimately text-only ("salt to taste").

**Data flow / scaling.** The sheet is opened from `RecipeViewPage`, which already holds the
items and servings multiplier. The page passes the **scaled** items into the sheet; scaling
stays where it lives today — neither the sheet nor the backend computes it.

**`useCopyRecipeToList.ts`** — arg-less mutation hook (spreads the generated
`copyRecipeToListMutation()`). `onSuccess` invalidates the target list's items query and the
list query (item count), keyed via `getListItemsQueryKey` / `getListQueryKey` from
`variables.body.targetListId`.

## Edge cases

- **No lists in household** → sheet empty-state with link to create a list.
- **Recipe with no items / all deselected** → action button disabled.
- **Invalid quantity** on a selected row → blocks the button; empty quantity allowed.
- **Stale recipe-item id** (deleted between load and submit) → backend skips it;
  `copiedCount` reflects reality.
- **Concurrent edits to target list** → append-only with fractional-index ranks; covered by
  the aggregate's existing rank-retry.

## Testing

Per repo rules, DB behavior goes through **Testcontainers IT**, never InMemory/SQLite.

- **`Frigorino.IntegrationTests` (Reqnroll + Postgres):**
  - Copy a subset of a recipe's items to a list → assert the right rows land with correct
    text, comment, scaled quantity, unchecked status; deselected items don't; a
    non-existent recipe-item id is skipped (not a 500).
  - Authorization: non-member / missing recipe / missing list → 404/403.
  - Playwright e2e: open recipe → actions menu → "Add to shopping list" → pick list →
    adjust a quantity → confirm → assert items appear on the list page. Assert on
    testids/`data-*`, never translated text.
- **`Frigorino.Test` (unit):** light slice-handler coverage for subset-filter / skip-missing
  only if cleanly separable from EF; otherwise leave to IT.
- **Frontend:** no JS test runner — manual browser verify via `/dev-up`; `npm run build`
  before the Playwright scenario (IT serves `ClientApp/build`).

## Verification gate (final)

`dotnet test Application/Frigorino.sln` (Test + IT) + frontend `npm run lint` / `tsc` /
`prettier` + `npm run api` (client regen) + `docker build`.
