# Recipes overview as a search hub

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-23
**Branch:** `feat/recipes-overview-search` (off `stage`)
**Feature doc:** `knowledge/Recipes.md`

## Summary

Turn the recipes overview (`/recipes`) from a flat newest-first list of bulky
text cards into a **scannable search hub**. Three moves:

1. **Search** — a text field at the top filters the list live, matching recipe
   **name + description + ingredient names**, with relevance ranking that floats
   name/description matches above ingredient-only matches.
2. **Denser cards with a cover photo** — one-line cards (thumbnail + name +
   one-line description + chevron) replace today's untruncated-description cards.
   This also fixes a layout bug (the item-count chip overlaps wrapping text).
3. **Inline peek** — a chevron expands a card in place to show the full
   description + ingredient list + an "Open recipe" action, so you can scan and
   decide without leaving the overview.

Mostly frontend, plus **one additive backend projection change** (no new
endpoint, no migration).

## Motivation

The overview is the entry point to recipes but offers no way to find one — no
search, fixed newest-first order, and each card dumps the full untruncated
description so only ~2 recipes fit on screen. The current `RecipeSummaryCard`
also pins the count chip + ⋮ menu as a MUI `secondaryAction`, which is
absolutely positioned and **overlaps the wrapping description** (visible bug).
A household's recipe collection is meant to be browsed and searched; the surface
should be built for that.

## Scope

In scope:
1. Backend: extend the recipe list projection with `coverAttachmentId` +
   `ingredients` (additive DTO fields; reuses the existing thumbnail stream
   endpoint). Regenerate the TS client.
2. Frontend: search field + client-side filter/ranking on `RecipesPage`.
3. Frontend: rework `RecipeSummaryCard` to the one-line + cover-thumbnail shape;
   fix the `secondaryAction` overlap.
4. Frontend: chevron-triggered inline peek (full description + ingredient chips +
   Open action + relocated ⋮ menu); one card expanded at a time.
5. New i18n keys (en + de).
6. Integration test coverage (Reqnroll + Playwright) for search/ranking + peek.

Out of scope (explicitly deferred):
- **Server-side search + pagination.** Client-side over the full list is the MVP;
  logged as tech debt for when collections reach the hundreds
  (`TECH_DEBT.md` → "Recipe search is client-side over the full list").
- **Tags / categories / sort control.** No new domain concept; no A–Z/sort UI.
  Empty-query order stays newest-first.
- **Copy-to-list shortcut inside the peek.** The peek opens the recipe; copy-to-list
  stays on the recipe view.
- Any change to the recipe **view/edit** pages, attachments, or AI extraction.

## Decisions (resolved during brainstorm)

| Decision | Choice |
|---|---|
| Search reach | Name **+** description **+** ingredient names. |
| Ranking | Tiered: name match > description match > ingredient match; tiebreak `createdAt` desc. |
| Where search runs | **Client-side** over the already-loaded list (instant; no backend search). MVP — see tech debt. |
| Empty-query order | **Newest-first** (unchanged). No separate sort control. |
| Cover image | Recipe's **first active Image attachment** thumbnail; fork-icon placeholder when none (a PDF-only recipe counts as "none"). |
| Card layout | **One-line** card: cover thumb + bold name + one-line truncated description + chevron. |
| Overlap bug | Fixed by **dropping `secondaryAction`** — chevron lives in normal flow; ⋮ moves into the peek. |
| Peek trigger | **Chevron expands** inline; tapping the name/photo still **opens the recipe view** (today's fast path intact). |
| Peek content | Full description + ingredient chips (matched ones highlighted while searching) + **Open recipe** primary + ⋮ menu. |
| Expanded count | **One card at a time.** |
| List DTO | **Reuse** `RecipeResponse.ToProjection` (both GET slices carry the new fields) rather than a dedicated list DTO — simplest; revisit if payload grows. |

## Backend

One file changes the data; everything else already exists.

### `RecipeResponse` (`Frigorino.Features/Recipes/RecipeResponse.cs`)

Add two fields to the record and the `ToProjection` expression:

- `int? CoverAttachmentId` — id of the recipe's first active **Image**
  attachment, ordered by `Rank`; `null` when the recipe has no image (no
  attachments, or only a PDF). The card builds the thumbnail URL from it via the
  existing `useAttachmentImage(householdId, recipeId, attachmentId, "thumbnail")`.
- `IReadOnlyList<string> Ingredients` — active `RecipeItem.Text`, ordered for
  stable display (section rank, then item rank). Feeds both the client-side
  search match and the peek's ingredient chips.

EF projection sketch (translatable):

```csharp
CoverAttachmentId = r.Attachments
    .Where(a => a.IsActive && a.Type == AttachmentType.Image)
    .OrderBy(a => a.Rank)
    .Select(a => (int?)a.Id)
    .FirstOrDefault(),
Ingredients = r.Items
    .Where(i => i.IsActive)
    .OrderBy(i => i.Section.Rank).ThenBy(i => i.Rank)
    .Select(i => i.Text)
    .ToList(),
```

Notes:
- `ToProjection` is shared with `GetRecipe` (single), so that response carries
  the fields redundantly — harmless and deliberately the lazy choice.
- No new endpoint. Thumbnails stream via the existing
  `GET /{attachmentId}/thumbnail` (1-year immutable cache already set).
- No migration — purely a read-side projection over existing columns.

### Client regeneration

After the DTO change, run `npm run api` from `ClientApp/` to regenerate
`src/lib/api` (the build-time MSBuild target emits `openapi.json`; no backend
boot needed). Commit the regenerated client.

## Frontend

### Search + ranking — `RecipesPage` + a pure util

- New local state on `RecipesPage`: `query` (search text) and
  `expandedRecipeId: number | null` (the one open peek).
- A search `TextField` (MUI, `size="small"`, search adornment) sits above the
  list. No debounce needed — filtering a small in-memory array is instant.
- Extract ranking into a pure function, e.g.
  `features/recipes/searchRecipes.ts`:
  `rankRecipes(recipes, query) => RecipeResponse[]`.
  - Case-insensitive substring match. Score tiers (highest wins):
    name match = 3, description match = 2, any ingredient match = 1.
  - Filter out score-0 recipes when `query` is non-empty; sort by score desc,
    tiebreak `createdAt` desc.
  - Empty `query` → return all, newest-first (preserves today's order; the list
    already arrives ordered that way).
- No-results: a simple centered "no matches" message (new i18n key) when
  `query` is non-empty and the ranked list is empty.

### Card rework — `RecipeSummaryCard`

Replace the MUI `List`/`ListItem`/`secondaryAction` structure (the source of the
overlap bug) with a plain flex card:

- **Collapsed (one line):** `[cover thumb] [name (bold, 1-line ellipsis) + description (1-line ellipsis)] [chevron]`.
  - Cover thumb: a small rounded square. A `RecipeCoverThumb` subcomponent calls
    `useAttachmentImage(householdId, recipeId, coverAttachmentId, "thumbnail",
    enabled = coverAttachmentId != null)` and renders the image, or a fork/utensils
    icon placeholder when there's no cover or while loading. (Reuses the
    StrictMode-safe blob→object-URL pattern already in `useAttachmentImage`.)
  - Tapping the name/thumb area → `onClick(recipe.id)` → navigate to
    `/recipes/$recipeId/view` (unchanged).
  - The chevron is its own button (`aria-expanded`, `stopPropagation`): toggles
    `expandedRecipeId` between this recipe and `null`.
- **Expanded (peek):** below the collapsed row, a bordered region with:
  - full description (no clamp; omitted if empty),
  - an "Ingredients" label + ingredient **chips** from `recipe.ingredients`;
    while searching, chips whose text contains the query get a highlighted
    (coral) style. Cap the visible chips (e.g. first ~8) with a `+N` overflow
    chip to keep the peek compact.
  - an action row: **Open recipe** (primary, navigates to `/view`) + the ⋮
    `RecipeActionsMenu` trigger (delete etc.) — relocated here from the
    collapsed row.

`RecipesPage` passes `expanded` + `onToggleExpand` to each card and keeps the
existing menu/delete wiring (`RecipeActionsMenu`, `DeleteRecipeConfirmDialog`)
unchanged — only the ⋮ button's location moves.

### Testids (for IT)

Keep/clarify stable testids on the card: `recipe-item-<name>` (open target),
`recipe-item-count-<name>`, `recipe-item-menu-button-<name>` (now inside the
peek), plus new `recipe-card-expand-<name>` (chevron),
`recipe-card-peek-<name>` (expanded region), and a `recipe-search-input` on the
search field. Assert on these, never on translated text.

### i18n (en + de)

New keys under the existing `recipes` namespace (JSON only — existing namespace,
so no `i18next.d.ts` change): search placeholder, no-results message, "Open
recipe", and an ingredients label if not already present.

## Testing

Per project convention, DB-touching/end-to-end behavior is covered in
`Frigorino.IntegrationTests` (Reqnroll + Playwright + Postgres Testcontainers);
there is no JS unit runner, so the ranking util is exercised through the E2E
search scenario rather than a unit test.

Scenarios (assert on testids / `data-*`, retrying `Expect(...)`):
1. **Search filters by name/description** — typing narrows the visible cards.
2. **Search matches an ingredient** — a recipe whose name/description don't
   contain the query but whose ingredient does still appears.
3. **Ranking** — when the query hits one recipe's name and another's ingredient
   only, the name match is ordered first.
4. **Peek** — the chevron expands the card, the ingredient chips render, and
   "Open recipe" navigates to the view; tapping the name (not the chevron) opens
   directly.

Reminder: the IT harness serves the SPA from `ClientApp/build`, so run
`npm run build` after the React changes or the new testids won't appear.

## Verification

- Frontend: `npm run lint`, `npm run tsc`, `npm run prettier`, `npm run api`
  (regen + commit client).
- Backend/E2E: `dotnet test Application/Frigorino.sln` (unit + integration).
- Final gate: `docker build -f Application/Dockerfile -t frigorino .` (catches
  pipeline/SPA/Dockerfile drift).

## Documentation

Update `knowledge/Recipes.md` (Frontend section) to describe the overview's
search + cover-thumbnail + peek behavior in the same change.
