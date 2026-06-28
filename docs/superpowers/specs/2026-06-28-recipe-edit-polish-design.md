# Recipe edit polish — collapse extras + compact rows — Design

**Date:** 2026-06-28
**Status:** Approved (pending spec review)
**Scope:** Recipe edit page only. The shared `SortableList`/`SortableListItem` change is opt-in (`dense`, default false), so Lists + Inventories are visually and behaviourally unchanged.

## Problem

The recipe edit page over-weights the *optional* extras and under-weights the actual recipe-building. The tag selector (`RecipeTagSelector`, currently inside `EditRecipeForm.tsx:241`) and the source links (`RecipeSourcesStrip`, `RecipeEditPage.tsx:259`) both sit above the fold, pushing the item composer + sections down. And the ingredient rows render through the shared `SortableListItem` card chrome — an outlined card per item with the quantity chip stacked *below* the name — so they read bulky next to the calmer header/sections/strip.

The approved (since-deleted) `RecipeEditPrototype` showed denser rows: name + small italic comment on the left, quantity pill right-aligned on the **same** line, hairline dividers instead of per-item cards. This spec brings the live page to that shape while keeping the per-row ⋮ menu.

## Decisions (locked during brainstorming)

1. **Keep the ⋮ menu** (sketch d). The prototype was menu-less (tap-to-edit), but dropping the menu would force rewriting the shared Reqnroll item steps (`ComposerSteps`, `RecipeSteps`, plus Lists/Inventories) that drive `item-menu-button-{text}` / `edit-item-button` / `delete-item-button`. Keeping the menu gets ~80% of the look for zero IT churn. **No IT changes in this work.**
2. **Lift the tag selector out, one "Details" accordion** (sketch a). The two extras live at different component levels today; unify them into a single page-level collapsed accordion. `EditRecipeForm`'s scope shrinks to name / servings / description + autosave status.
3. **Compact row content is recipe-only** (sketch b) — `RecipeItemContent.tsx` only.
4. **Compact row chrome is an opt-in `dense` prop on the shared components** (sketch c) — default false; only `RecipeContainer` passes it.
5. **The `dense` editing/processing pulse stays** — only the *resting* row loses its card; the transient editing (warning) and processing (primary) pulses are `boxShadow`-based and keep working without the static border.
6. **The description field stays visible** — it is core metadata, not an "extra". Only tags + sources go behind the accordion.

## (a) Collapse extras → one "Details" accordion

### `EditRecipeForm.tsx`

Remove `<RecipeTagSelector householdId={householdId} recipe={recipe} />` (line 241). The form now ends at the autosave-status box; its responsibility is name / servings / description. No other change — the `householdId` prop is still used by the rest of the form, so no signature churn.

### `RecipeEditPage.tsx`

Between `<EditRecipeForm>` and `<SortableSectionList>` in the page `<Stack>`, render one collapsed accordion containing both extras:

```tsx
<Accordion disableGutters elevation={0} sx={{ /* flush with theme; no extra card */ }}>
    <AccordionSummary expandIcon={<ExpandMore />} data-testid="recipe-details-accordion">
        <Typography variant="subtitle2">{t("recipes.details")}</Typography>
    </AccordionSummary>
    <AccordionDetails>
        <RecipeTagSelector householdId={householdId} recipe={recipe} />
        <RecipeSourcesStrip householdId={householdId} recipeId={recipeId} />
    </AccordionDetails>
</Accordion>
```

- Default **closed** (no `defaultExpanded`).
- No API change — tag suggestion still runs behind the fold (the component mounts collapsed; MUI `Accordion` keeps children mounted by default, so the existing suggestion fetch behaves exactly as before). If we want to defer the fetch until expand, that is a follow-up, not this spec.
- Styling follows `knowledge/Frontend_Styling.md` — lean on the theme; `elevation={0}` + `disableGutters` so the accordion reads as a flush section, not a competing card.

### i18n

Add `recipes.details` to `public/locales/{en,de}/translation.json` (existing namespace — JSON only, no `i18next.d.ts` change needed for an existing top-level namespace). EN ≈ "Details", DE ≈ "Details".

## (b) Compact row content — recipe-only

### `RecipeItemContent.tsx`

Replace the `ListItemText` primary/secondary stack with a flex row so the quantity sits on the name's line:

- Root: `<Box>` with `display: flex`, `width: 100%`, `alignItems: flex-start`, `justifyContent: space-between`, `gap`.
- **Left column** (`flex: 1`, min-width 0 for ellipsis safety): name `Typography` (`body2`, `fontWeight: 500`, `wordBreak: break-word`) on top; when `item.comment`, an italic caption underneath (`variant="caption"`, `fontStyle: italic`, `color: text.secondary`, `whiteSpace: pre-wrap`, `wordBreak: break-word`).
- **Right:** `ItemQuantityChip` when `item.quantity`, aligned to the top so it tracks the name line.
- **Testids preserved exactly:** `recipe-item-{item.id}` on the root, `recipe-item-comment-{item.id}` on the comment caption, `recipe-item-quantity-{item.text}` on the chip (via its `testId` prop).

No change to `RecipeContainer`'s `renderContent={(item) => <RecipeItemContent item={item} />}` wiring.

## (c) Compact row chrome — shared, opt-in

A new `dense?: boolean` (default `false`) threads through two shared files; `SortableItem` is untouched because `containerSx` flows straight through it.

### `SortableList.tsx`

- Add `dense?: boolean` to `SortableListProps` (default `false`).
- Forward `dense={dense}` to every `<SortableListItem>` (both the unchecked and the checked render — recipes only ever populate unchecked, but forwarding to both keeps the prop honest).
- When `dense`, drop the inter-row margin: the `<List sx={{ ..., "& .MuiListItem-root": { mb: 0.5 } }}>` rule on the unchecked list becomes `mb: 0` (or omitted) under dense. This is the **second** source of row gap, in addition to the per-item `containerSx mb` below.

### `SortableListItem.tsx`

- Add `dense?: boolean` to `SortableListItemProps` (default `false`).
- Build `containerSx` conditionally:
  - **Not dense (current behaviour, unchanged):** `border: 1px solid` card, `borderRadius: 1`, `mb: 0.5`, `boxShadow` per state.
  - **Dense:** no full border / radius / `mb`; instead `borderBottom: "1px solid"` with `borderColor: divider` (hairline divider), `borderRadius: 0`. The editing (`warning.main`) and processing (`primary.main`) states still apply their pulse `animation` + `boxShadow` and may tint `borderBottomColor`; the `bgcolor` editing tint (`warning.50`) is preserved.
- The inner `ListItem`/`ListItemButton` padding (`py: 0.75`, `px: 0.75`) already reads as "denser layout" per its own comments — leave as-is, or tighten `py` slightly under dense if needed during implementation (polish, not a hard requirement).

### `RecipeContainer.tsx`

Pass `dense` to `<SortableList>`. Nothing else changes; `showDragHandles`, `renderContent`, reorder/delete wiring all stay.

**Lists + Inventories:** their `SortableList` usages do not pass `dense`, so they default to `false` → byte-for-byte the same chrome. Confirm with a visual glance during verification.

## (d) ⋮ menu — kept

No change to the menu, its handlers, or its testids. No Reqnroll step changes.

## Testing

- **No new unit tests.** This is presentational; the domain and slices are untouched. `Frigorino.Test` is unaffected.
- **No new / changed integration tests.** All driven testids are preserved (`recipe-item-*`, `item-menu-button-*`, `edit-item-button`, `delete-item-button`, `recipe-servings-*`, `recipe-name-input`, `recipe-description-input`). The new `recipe-details-accordion` testid is additive. Existing recipe + list + inventory item ITs should pass unchanged.
- **Manual browser verify** (the real net for a visual change — static checks won't catch layout/runtime bugs):
  - Recipe edit page: "Details" section collapsed by default; expanding reveals tag selector + sources strip; tag suggestion still works behind the fold.
  - Ingredient rows are single-line dense (name + italic comment left, quantity pill right), hairline dividers, no per-item cards; ⋮ menu still edits + deletes; editing pulse + processing pulse still visible.
  - Lists + Inventories item rows look **identical** to before (card chrome intact).
- **Verification gate:** `npm run build` (the IT harness serves `ClientApp/build`, and the new testid must land there) → `npm run lint` + `npm run tsc` + prettier → `dotnet test Application/Frigorino.sln` (full SLN, since a shared component changed) → `docker build` as the final drift check.

## Impact / cost

Small, presentational, reversible. No backend, no migration, no API regeneration, no new dependency. Touches ~5 frontend files: `EditRecipeForm.tsx` (remove a line), `RecipeEditPage.tsx` (add the accordion), `RecipeItemContent.tsx` (flex-row rewrite), `SortableList.tsx` + `SortableListItem.tsx` (thread `dense`), `RecipeContainer.tsx` (pass `dense`), plus the two i18n JSON files.

## Out of scope / noted smells

- **Menu-less tap-to-edit** (the full prototype interaction) — deferred; would require the shared item-step IT rewrite. Revisit only if the menu proves to be the remaining bulk after this lands.
- **`SortableList` empty state is hardcoded German** (`"List ist leer"` / `"Füge deinen ersten Artikel hinzu…"`, ~line 432) — a pre-existing i18n gap, surfaced here but not fixed in this presentational pass.
- **Deferring the tag-suggestion fetch until accordion expand** — possible perf nicety, but not needed; the fetch is already cheap and behind the fold. Not in scope.
