# Recipe sections (multi-part recipes)

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-14
**Branch:** `feat/recipe-sections`
**Source idea:** "Sections (multi-part recipes)" entry in `IDEAS_Recipes.md`
**Predecessor specs:** `2026-06-14-recipes-feature-design.md` (MVP), `2026-06-14-recipe-metadata-servings-design.md` (servings/scaling)

## Summary

Real recipes have parts — crust / filling, dough / sauce. Today a recipe is a single flat
ingredient list. This adds an ordered **`RecipeSection`** entity; every `RecipeItem` belongs to a
section, item rank becomes **per-section**, and the editor/view group items under section headers.
A section carries an optional **name** and an optional **multiline description**. The single
recipe-level servings stepper continues to scale **all** sections.

The MVP schema was shaped to absorb this; item ordering uses fractional indexing, so making rank
per-section is a re-scope of the unique index, not a rewrite.

## Scope

In scope:
1. `RecipeSection` entity (name?, description?, fractional rank) + `SectionId` FK on `RecipeItem`.
2. One-time migration that backfills a default section per existing recipe and assigns its items.
3. Section CRUD + reorder slices; `Recipe` aggregate methods for sections.
4. Item create takes a target `SectionId`; item reorder constrained to within its own section.
5. Editor: accordion of section cards (one open at a time), per-section description, section drag
   reorder, "+ Section" affordance, composer targets the open section.
6. View: sequential section headers + descriptions; the existing single servings stepper scales all.

Out of scope (explicitly deferred):
- **Cross-section item moves via DnD** — items only reorder within their own section (keeps the drag
  model simple). Moving an item between sections is not supported in this cut.
- **Per-section servings / yield** — one `Recipe.Servings`, one multiplier across all sections.
- Promote-to-shopping-list, unit conversion — unchanged parked ideas.

## Decisions (resolved during brainstorm)

| Decision | Choice |
|---|---|
| Item↔section relation | **Required** `SectionId` + backfill migration. Rank becomes per-section. |
| Section name | **Optional**; empty name renders as the localized "Zutaten"/"Ingredients" header. |
| Default / first section | Auto-created (unnamed) on `CreateRecipe`; single-section recipes look identical to today. |
| Min sections | A recipe always has **≥ 1 section**; the last remaining section cannot be deleted. |
| Section reorder | **Yes** — sections have their own fractional rank + reorder endpoint and a drag handle. |
| Delete non-empty section | **Cascade soft-delete** section + its items, with an **undo toast** restoring both. |
| Cross-section item DnD | Not allowed — `ReorderItem`'s `afterId` must be in the same section. |
| Servings scaling | Unchanged — one recipe-level stepper, one multiplier, applied across all sections. |
| Details collapsible | Stays an **independent** collapsible above the section accordion (not part of it). |
| Empty sections in view | Hidden (a section with no items and no description is not rendered in the read-only view). |
| Section name max | 100 chars (nullable). |
| Section description max | 2000 chars (nullable, multiline). |

## Backend changes

### Domain — new entity `RecipeSection` (`Frigorino.Domain/Entities/RecipeSection.cs`)
- Fields: `Id`, `RecipeId`, `Name` (nullable, `NameMaxLength = 100`), `Description` (nullable,
  `DescriptionMaxLength = 2000`), `Rank` (fractional-index `string`), `CreatedAt`, `UpdatedAt`,
  `IsActive`, nav `Recipe`, `ICollection<RecipeItem> Items`.
- Validation in the aggregate methods (name/description length, with `"Property"` error metadata),
  mirroring `Recipe`'s existing validation style.

### Domain — `Recipe` aggregate (`Frigorino.Domain/Entities/Recipe.cs`)
- Add `ICollection<RecipeSection> Sections`.
- New methods (all return `FluentResults.Result<T>`):
  - `AddSection(name?, description?)` — mints rank after the last active section.
  - `UpdateSection(sectionId, name?, description?)` — validates; `EntityNotFoundError` if missing.
  - `RemoveSection(sectionId)` — **cascade**: soft-deletes the section and all its active items.
    Returns an error if it is the **last** active section.
  - `RestoreSection(sectionId)` — reactivates the section and its items; re-mints colliding ranks
    (item ranks within the section, section rank among sections) — mirrors `RestoreItem`'s pattern.
  - `ReorderSection(sectionId, afterSectionId)` — fractional rank between neighbours; `afterSectionId == 0`
    means top.
- Change existing item methods:
  - `AddItem(sectionId, text, quantity, comment)` — mints rank within the **target section**;
    `EntityNotFoundError` if the section isn't part of the recipe.
  - `ReorderItem(itemId, afterItemId)` — `afterItemId` must belong to the **same section** as the item
    (else error); rank minted within that section.
  - `RestoreItem` — re-mint within the item's section.

### Persistence
- New `RecipeSectionConfiguration`: `Rank` `text` + `UseCollation("C")` required; FK to `Recipe`
  cascade; partial unique index `{RecipeId, Rank}` filtered `IsActive = true`; indexes on `RecipeId`,
  `IsActive`, `{RecipeId, IsActive}`. Mirrors `RecipeConfiguration`.
- `RecipeItemConfiguration`: add `SectionId` FK (→ `RecipeSection`, `OnDelete(Cascade)`); **change the
  partial unique index from `{RecipeId, Rank}` to `{SectionId, Rank}`** (filtered `IsActive = true`);
  add `SectionId` and `{SectionId, IsActive}` indexes.
- `ApplicationDbContext`: add `DbSet<RecipeSection>`; auto-timestamp it in `SaveChangesAsync` like the
  other entities. Add `RecipeSection` to the `DeleteInactiveItems` purge cascade where appropriate.

### Migration — `AddRecipeSections`
1. Create `RecipeSections` table.
2. Add **nullable** `SectionId` column to `RecipeItems`.
3. **Backfill (raw SQL):** for every active recipe, insert one default section (name `NULL`, an initial
   fractional rank, timestamps now), then `UPDATE RecipeItems SET SectionId = <that section>` for all
   the recipe's items. Create the default section even for recipes with zero items, so the editor
   always has one.
4. Alter `SectionId` to **non-nullable**, add the FK, drop the old `{RecipeId, Rank}` unique index on
   `RecipeItems` and create the new `{SectionId, Rank}` one.

> Note the [[reference_ef_isnull_pruned_on_required_column]] gotcha: do the backfill in raw SQL inside
> the migration (the column is about to be required), not via LINQ.

### Slices (new folder `Frigorino.Features/Recipes/Sections/`)
- `CreateRecipeSection` — `POST /api/household/{householdId}/recipes/{recipeId}/sections`
  `{ Name?, Description? }` → `RecipeSectionResponse`.
- `UpdateRecipeSection` — `PUT .../recipes/{recipeId}/sections/{sectionId}` `{ Name?, Description? }`.
- `DeleteRecipeSection` — `DELETE .../sections/{sectionId}` (soft-delete + cascade; last-section guard).
- `RestoreRecipeSection` — `POST .../sections/{sectionId}/restore`.
- `ReorderRecipeSection` — `PATCH .../sections/{sectionId}/reorder` `{ AfterId }`, wrapped in
  `RankRetry.SaveWithRetryAsync`.
- `GetRecipeSections` — `GET .../recipes/{recipeId}/sections` → ordered `RecipeSectionResponse[]`.
- `RecipeSectionResponse` — `{ Id, Name?, Description?, Rank }`.

### Item slice changes
- `CreateRecipeItem`: add `SectionId` to the request body; pass to `recipe.AddItem(sectionId, …)`.
  (Other item routes stay item-id-based — item id is globally unique, and the item already knows its
  section.)
- `ReorderRecipeItem`: unchanged shape; the same-section constraint is enforced in the aggregate.
- `RecipeItemResponse`: add `SectionId` so the frontend can group.
- `GetRecipeRevision`: extend the revision token to also fold in **max section `UpdatedAt`** and the
  **active section count**, so section edits/reorders/deletes trigger sync invalidation.

## Frontend changes

### Editor — `RecipeEditPage.tsx`
- Keep **Details** as its own independent collapsible at the top.
- Replace the single ingredients collapsible with a **list of section cards** rendered as an
  **accordion: only one section expanded at a time** (opening one collapses the others). Persist the
  open-section id via the existing `usePersistedExpanded` pattern (single value, not a set).
- New `RecipeSectionCard` component per section: header with **inline-editable name** (placeholder =
  localized default header), expand caret, **drag handle** (section reorder via `SortableList`),
  overflow menu (rename / delete), a **multiline description** field, and the item list
  (`RecipeContainer` scoped to the section's items, DnD within section).
- **Composer (`RecipeFooter`) targets the open section** — it passes that `sectionId` to
  `useCreateRecipeItem`. When no section is expanded, the composer is hidden.
- **"+ Section"** button below the list → `useCreateRecipeSection`, then auto-expands the new section.
- Section reorder is its own `SortableList` over the section cards; item reorder stays a `SortableList`
  within each card.

### View — `RecipeViewPage.tsx` / `RecipeViewList.tsx`
- The **servings stepper stays at the top** and produces one `multiplier` (unchanged).
- `RecipeViewList` becomes **grouped**: iterate active sections in rank order → render a section header
  (name, or the localized default header) + the description (if any) + the section's ingredient table.
  The `multiplier` is applied to every item across all sections (unchanged `scaleQuantity`).
- **Hide** sections that have no items and no description.
- The existing search filter applies across all sections; sections with no matches are omitted.

### Hooks (`src/features/recipes/`)
- New: `useRecipeSections` (query, `enabled` on ids `> 0`, staleTime mirroring `useRecipeItems`),
  `useCreateRecipeSection`, `useUpdateRecipeSection`, `useDeleteRecipeSection` (undo toast →
  `useRestoreRecipeSection`), `useRestoreRecipeSection`, `useReorderRecipeSection` (optimistic
  reposition mirroring `useReorderRecipeItem`).
- `useCreateRecipeItem`: add `sectionId` to the body; the optimistic temp item carries its `sectionId`
  (keep the [[project_optimistic_tempid_reconcile]] temp-id swap in `onSuccess`).
- `useReorderRecipeItem`: optimistic reposition now operates within the item's section group.
- `useRecipeRevision`: unchanged call site — it already drives `useRevisionInvalidation`; the token now
  reflects section changes so both `getRecipeSections` and `getRecipeItems` queries revalidate.
- Run `npm run api` after the DTO changes to regenerate the client.

## Testing

- **Aggregate unit tests** (`Frigorino.Test`): `AddSection` ranks at end; `RemoveSection` cascades
  items and is blocked on the last section; `RestoreSection` reactivates section + items; `ReorderSection`
  ranks between neighbours; `AddItem` rejects an unknown section; `ReorderItem` rejects a cross-section
  `afterId`; section name/description length validation.
- **Integration tests** (`Frigorino.IntegrationTests`, Reqnroll + Postgres Testcontainers): create a
  second section, add items to each, reorder a section, delete a non-empty section and restore it,
  assert items round-trip with the right `sectionId` and per-section order. **Run `npm run build`**
  before IT so new testids are served from `ClientApp/build`.
- **Frontend** (no JS runner) — verify via dev-up / Playwright MCP: accordion opens one at a time,
  composer adds to the open section, section drag reorders, view groups sections with descriptions and
  one stepper scales all, undo restores a deleted section.

## Non-goals recap

No cross-section item moves, no per-section servings, no promote wiring, no unit conversion. This is a
sectioning layer over the existing flat item list, sized to land independently on top of the recipes
MVP + servings work.
