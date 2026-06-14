# Recipe view/edit refinement: read-only view + dedicated edit page

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-14
**Branch:** `feat/recipe-metadata-servings` (continues on the same branch as the metadata feature)
**Predecessor spec:** `docs/superpowers/specs/2026-06-14-recipe-metadata-servings-design.md`

## Summary

Refine the recipe UX so **viewing and editing are separate pages**. Today the
recipe "view" page (`/recipes/$recipeId/view`) is actually the full ingredient
**editor** (always-on composer footer, drag-to-reorder handles, per-item
edit/delete), while the "edit" page (`/recipes/$recipeId/edit`) only edits
**metadata** (name/description/servings). Recipes are created once and read many
times, so the default should be a calm read-only view.

After this change:

- **`/view`** is purely **read-only** — a clean recipe to cook from. No
  composer, no drag handles, no per-item actions.
- **`/edit`** grows from metadata-only into the **single editing surface** —
  metadata form **plus** the full ingredient editor that used to live on the
  view page.

This is purely a frontend restructure. **No backend, API, DB, or DTO changes.**

## Motivation

Separating into two pages (rather than an in-page edit-mode toggle) was chosen
so the read-only view can be evolved freely later (a "nicer view experience")
without risk of breaking the edit machinery, and so all editing concerns live in
one place. (User decision during brainstorm.)

## Scope

In scope (frontend only):
1. `RecipeViewPage` → read-only recipe view (new read-only ingredient rendering).
2. `RecipeEditPage` → metadata form + the migrated ingredient editor.
3. Description moves out of the header subtitle into its own region on `/view`.
4. Create flow lands on `/edit` instead of `/view`.
5. Dead-code cleanup of view-only props that become unused after the move.
6. New i18n keys (en + de).

Out of scope (explicitly deferred):
- Any backend/API/DB/migration change.
- "Cook mode" (tap-to-check ingredients while cooking) — a future view nicety.
- Non-integer scaled quantities for count units (e.g. "4.5 Stück") — display
  as-is for now; noted as a future polish.
- Fixing the post-create back-navigation issue — logged separately in `BUGS.md`
  ("Back navigation misbehaves after creating data…"); do **not** debug it here,
  but the new create→`/edit` redirect must use `replace: true` (see below).

## Decisions (resolved during brainstorm)

| Decision | Choice |
|---|---|
| View/edit separation | **Two separate pages** (not an in-page mode toggle). |
| Read-only ingredient layout | **Quantity column (left) + name (right)**, recipe-style — not chips. |
| Edit entry point on `/view` | **Pencil icon** as a direct (section-colored) action in the header; Search moves into the ⋮ menu. |
| Description placement on `/view` | Its **own italic band** under the action bar, above the servings scaler. |
| Servings scaler | Stays **view-only**; grouped with a "Zutaten" section heading; Reset link kept (shown only while scaled). |
| Search | **Kept** on `/view`, reached from the ⋮ menu; filters ingredients. |
| Post-create landing | **`/edit`** (recipe has a name but no ingredients yet). |
| Edit page save model | **Unified implicit save-on-change** across the whole edit view — metadata fields auto-save (debounced); ingredient edits are already immediate. **No Save/Cancel buttons**; the back arrow is the only exit. |

## Read-only view — `RecipeViewPage`

Layout, top to bottom (keep the existing full-height flex column shell):

1. **Action bar** (`PageHeadActionBar`):
   - Title = recipe name; **no `subtitle`** anymore (description moves out).
   - **Direct action:** a pencil (`Edit`) icon → `router.navigate` to
     `/recipes/$recipeId/edit`. This is the existing `handleEdit`, promoted from
     a menu item to `directActions[0]` so it takes the section color. Keep its
     `testId: "recipe-edit-button"`.
   - **Menu (⋮):** Search toggle (existing `handleToggleSearch`,
     `testId: "recipe-search-button"`).
2. **Description band** (only when `recipe.description` is set): a full-width
   region rendering the complete description (italic, `text.secondary`,
   `whiteSpace: pre-wrap`, word-break). Not truncated. `data-testid="recipe-description"`.
3. **Search row** (`SearchInputRow`, existing) — unchanged behavior.
4. **Zutaten section header + scaler** (only when `recipe.servings != null`):
   - Left: section label `t("recipes.ingredientsHeading")` ("Zutaten").
   - Right: the existing scaler controls — Reset link (only while scaled),
     `[−] N [+]` stepper. Keep all existing testids
     (`recipe-servings-reset/decrement/value/increment`).
   - Sub-line beneath: `t("recipes.servingsFor", { count: baseServings })`
     ("für N Portionen"); when scaled, append/replace with
     `t("recipes.scaledFrom", { count: baseServings })` cue. Keep
     `data-testid="recipe-servings-value"` on the number.
   - When `recipe.servings == null`: no scaler, but still show a plain "Zutaten"
     heading above the list (no stepper).
5. **Read-only ingredient list** — a **new** component (see below). No
   `SortableList`, no drag handles, no swipe/edit/delete, no footer composer.

The view page **no longer** manages `editingItem`, item add/update handlers,
`useCreateRecipeItem`/`useUpdateRecipeItem`, extraction polling
(`useRecipeExtractionPoll`/`pendingExtraction`), `RecipeFooter`, or the
edit-capable `RecipeContainer`. It keeps: `useRecipe`, `useRecipeItems`,
`useRecipeRevision`, search state, and the scaling state (`targetServings` +
derived `baseServings`/`effectiveServings`/`isScaled`/`multiplier`/`stepServings`).

### New component: read-only ingredient rendering

Create two focused files under `features/recipes/items/components/`:

- **`RecipeViewList.tsx`** — takes `items`, `multiplier`, `searchQuery`. Applies
  the same search filter as `RecipeContainer` (`matchesQuery` over
  text + comment), renders the no-match state
  (`data-testid="recipe-search-no-results"`) and an **empty state** when the
  recipe has no ingredients (a hint pointing to Edit,
  `data-testid="recipe-empty"`). Maps visible items to `RecipeViewItem`. Container
  is the scroll region (`flex:1, overflow:auto`), `data-testid="recipe-items"`.
- **`RecipeViewItem.tsx`** — one read-only row: a fixed-width **quantity column**
  (left) + **name** (right), comment as a caption under the name. Quantity is
  rendered as **text** via `formatQuantity` (not `ItemQuantityChip`).
  - Scaling cue (when `multiplier !== 1` and the item has a quantity): show the
    **scaled** value (via `scaleQuantity`, accent color e.g. `success.dark`/
    `primary`) with the **struck-through original beneath** it (small, disabled).
    Keep testids: row `recipe-item-${id}`, comment `recipe-item-comment-${id}`,
    scaled-original `recipe-item-quantity-base-${id}`, and a quantity testid for
    assertions.
  - Items without a quantity: render the name only (empty/spacer quantity column).

The scaling logic (`scaleQuantity`, multiplier, struck original) therefore
**moves** out of `RecipeItemContent` into `RecipeViewItem`.

## Edit page — `RecipeEditPage`

Becomes the single editing surface. Full-height flex column shell (like the old
view page), top to bottom:

1. **Action bar** (`PageHeadActionBar`): title `t("recipes.editRecipe")`, back
   arrow, ⋮ menu with **Delete recipe** (existing `menuActions` +
   `DeleteRecipeConfirmDialog`, unchanged).
2. **Scroll region** (`flex:1, overflow:auto`) containing, in order:
   - **`EditRecipeForm`** (metadata: name → description → servings), existing
     component, **converted to implicit save-on-change**. See "save model" below.
   - **Ingredient editor list** — the existing `RecipeContainer` (interactive:
     `SortableList` with drag handles, per-item edit/delete, reorder), moved here
     from the view page. It renders items via the existing `RecipeItemContent`
     (chip style — unchanged for the editor).
3. **Pinned footer:** `RecipeFooter` (the composer for add/edit items), moved
   here from the view page.

The edit page now owns the state the view page shed: `editingItem`,
`handleAddItem`/`handleUpdateItem`, `useCreateRecipeItem`/`useUpdateRecipeItem`,
extraction polling, and `scrollToLastItem`.

**Layout mechanic (the tricky bit):** today `RecipeContainer` is itself the
scroll container (`flex:1, overflow:auto`). On the edit page the metadata form
**and** the item list must scroll together above the pinned composer. Resolve by
making the metadata form + `RecipeContainer` share one outer scroll region:
give `RecipeContainer` a way to not be its own scroller when embedded (e.g. a
`scrollable=false` prop, default `true` to preserve any other usage), or wrap
both in an outer scroll `Box` and relax the container's overflow. The plan must
specify the exact approach and verify the composer stays pinned and the list
still scrolls.

**Save model — unified implicit save-on-change:** the entire edit view persists
as you go; there are **no Save/Cancel buttons** and no save-then-navigate. The
back arrow is the only way out, and everything is already saved when you leave.

- **Ingredients:** unchanged — immediate optimistic mutations (add/edit/delete/
  reorder), as today.
- **Metadata (`EditRecipeForm`):** fields are still seeded once on mount (keyed
  by `recipe.id`). On change, **debounce** (~600 ms; no generic debounce hook
  exists, so use a small inline debounced effect / `setTimeout` cleared per
  change, fired via the existing `useUpdateRecipe`) then PUT the current
  `{ name, description, servings }`. Also **flush on blur** so a quick edit +
  immediate back doesn't drop the last change (and clear any pending timer on
  unmount).
  - **Validation gates the save** (don't PUT invalid data): skip the save while
    `name` is empty (show the existing required-error inline; the last valid name
    stays on the server). Skip the save when `servings` is non-empty but not an
    integer in `1..99` (show an inline error). Empty servings → save as `null`.
    This is stricter than the create form's non-enforced native min/max, and is
    intentional.
  - **Feedback:** keep it light — a subtle saving/saved status indicator next to
    the fields (e.g. `data-testid="recipe-metadata-status"` toggling
    saving→saved) is enough; no toast. Remove the `recipe-edit-save-button`
    testid and the Save/Cancel buttons entirely.

Remove `EditRecipeForm`'s `handleSave`/`handleCancel` + `router.history.back()`
buttons; navigation off the page is solely the action-bar back arrow.

## Create flow

`CreateRecipeForm.tsx`: on successful create, navigate to
`/recipes/$recipeId/edit` (was `/view`) **with `replace: true`** so the create
route is not left on the history stack (mitigates the back-nav issue logged in
`BUGS.md`; does not fix the general bug). Keep `recipeId: response.id.toString()`.

## Dead-code cleanup (verify with grep, remove if unreferenced)

After the move, these view-only props likely become unused:
- `multiplier` on `RecipeContainer` and `RecipeItemContent` (scaling is now in
  `RecipeViewItem`; the edit-side list never scales) → remove.
- The scaling branch inside `RecipeItemContent` (struck original, `scaleQuantity`
  import) → remove (logic lives in `RecipeViewItem`).
- `color` prop on `ItemQuantityChip` (added for the chip scaling accent; the
  read-only view uses text, the editor uses unaccented chips) → remove **iff**
  no remaining caller passes it.

Keep `scaleQuantity` (now used by `RecipeViewItem`) and its barrel export.

## i18n (en + de)

Add under `recipes`:
- `ingredientsHeading` — "Ingredients" / "Zutaten".
- `servingsFor` — "for {{count}} servings" / "für {{count}} Portionen".
- `scaledFrom` — "scaled from {{count}}" / "skaliert von {{count}}".
- `emptyIngredients` — empty-state hint, e.g. "No ingredients yet — tap edit to
  add some." / "Noch keine Zutaten — zum Bearbeiten tippen."

Reuse existing `resetServings`, `servingsFrom`, `description`,
`descriptionPlaceholder`, `editRecipe`. Remove `servingsFrom` only if fully
replaced by `servingsFor` (verify usages).

## Testing

- **No new backend/unit tests** (no backend change).
- **Integration (Reqnroll + Playwright)** — update existing recipe UI scenarios
  for the new flow; assert on testids only (never translated text):
  - View page is read-only: composer/footer and drag handles are **not** present
    on `/view`; pencil `recipe-edit-button` navigates to `/edit`.
  - Edit page hosts both metadata fields and the ingredient editor (composer
    present, items addable/editable). **No Save button** — editing a metadata
    field (e.g. description or servings) then navigating back to `/view` shows
    the change persisted (auto-save). Wait out the debounce / trigger blur before
    navigating.
  - Create lands on `/edit` (URL/edit-affordances present after create).
  - Description renders in `recipe-description` on `/view` (not the header).
  - Scaling still works on `/view`: stepper appears only when servings set,
    changing it scales the quantity text, `recipe-servings-reset` appears only
    while scaled, base/struck original shown.
  - Rebuild the SPA (`npm run build`) before running IT (the harness serves
    `ClientApp/build`).
- **Manual** (dev-up + Playwright MCP): the full view↔edit round-trip, the
  quantity-column read-only layout, scaling cue, search from the ⋮ menu,
  create→edit landing.

## Non-goals recap

No backend changes. No cook-mode. No back-nav bug fix (logged, mitigated via
`replace`). No non-integer-count rounding. A pure frontend restructure that
makes the recipe view read-only and consolidates all editing onto `/edit`.
