# Recipe tags (Course + Dietary) with on-demand AI suggestion

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-23
**Branch:** `feat/recipes-tags` (off `stage`)
**Feature doc:** `knowledge/Recipes.md`, `knowledge/AI_Classification.md`

## Summary

Give recipes a curated, multi-select **tag** set so the overview can be filtered
by category. Two facets in one flat `RecipeTag` enum: **Course** (Breakfast,
Main, Dessert…) and **Dietary** (Vegetarian, Vegan, GlutenFree…). Tags are
selected on the recipe edit page, shown on the view page, and exposed as
**filter chips** on the overview that combine with the existing search.

Optionally (gated, off by default) an **AI "Suggest tags" button** on the edit
page runs a classifier over the recipe's name + description + ingredients and
returns suggested tags as **ghost chips** the user taps to accept. Suggestions
are **stateless and on-demand** — nothing is persisted, there is no suggestion
lifecycle, and AI never overwrites a user's tags.

Ingredient filtering ("does it have chicken?") is **explicitly not** a tag
concern — it is already served by the existing free-text overview search
(`rankRecipes` already ranks ingredient matches).

## Motivation

Client requirement #3: filter recipes by course. The overview already has search
(name + description + ingredient) but no category axis — you cannot say "show me
the desserts" or "show me the vegetarian mains". A small curated tag vocabulary
plus filter chips delivers exactly that without a free-form tagging system.

The AI angle mirrors the request to make recipe classification "similar to the
list items" — but list-item classification writes to a **non-user-facing**
`Product` catalog, so it can run fully automatically. Recipe tags are
**user-facing and user-editable**, so a silent auto-classifier would fight the
user's own choices. The chosen model keeps the user in control: AI only ever
*suggests*, on explicit demand.

## Scope

In scope:
1. Domain: `RecipeTag` enum (Course + Dietary) + `Recipe.Tags` value-set +
   `Recipe.SetTags(...)` aggregate method.
2. Storage: one `integer[]` column on `Recipes` (one migration).
3. Backend slices: `PUT /recipes/{id}/tags` (set whole set) and
   `POST /recipes/{id}/suggest-tags` (synchronous AI suggestion).
4. `RecipeResponse` gains `Tags` (additive projection field). Regenerate TS client.
5. Infrastructure: `IRecipeTagSuggester` port + `OpenAiRecipeTagSuggester` /
   `NullRecipeTagSuggester` + `AddRecipeTagSuggestion` DI, gated on a new
   `Ai:RecipeTagSuggester:*` config block, off by default.
6. Frontend: edit-page tag multi-select + "Suggest tags" button + ghost chips;
   view-page tag chips; overview tag-filter chip row (client-side AND filter
   combined with search). New i18n labels (en + de).
7. Tests: `SetTags` aggregate unit tests; integration coverage for tag
   round-trip + overview filter; suggest endpoint with a fake suggester.

Out of scope (explicitly deferred):
- **Cuisine tags** (Italian/Mexican/…). Open-ended; the curated-list bet is
  weakest there. Revisit if Course + Dietary proves limiting.
- **Free-form / per-household custom tags.** Considered and set aside in favour
  of the fixed curated set (logged in `IDEAS.md` if it recurs).
- **Server-side tag filtering.** Filtering runs client-side over the loaded list,
  same as the existing search (same household-scale tradeoff, already tracked in
  `TECH_DEBT.md`).
- **Persisted AI suggestions / auto-tagging / re-suggest lifecycle.** Suggestions
  are ephemeral; no stored suggestion state, no automatic classification on
  create/update.
- **AI → Product chaining.** Recipe tag suggestion writes no `Product` rows,
  consistent with the existing "recipes don't classify" rule.

## Decisions (resolved during brainstorm)

| Decision | Choice |
|---|---|
| Taxonomy | One flat `RecipeTag` enum, two facets: **Course + Dietary**. Cuisine deferred. |
| Ingredient filtering | **Not a tag** — stays in the existing free-text search. |
| Storage | **`integer[]` column** on `Recipes` (value-set; no join table, no rank/soft-delete). Fallback: value-converted delimited string — same column, no new table. |
| Set semantics | `SetTags` replaces the whole set (matches a multi-select), de-duped, validated, capped ≤ 10. |
| Write gate | Same as `Recipe.Update` — creator or Admin+ (`CanBeManagedBy`). |
| AI behavior | **Suggest only, on explicit demand, stateless.** No persistence, no auto-tag, no overwrite. |
| AI delivery | **Synchronous** `POST /suggest-tags` (user taps → spinner → chips). Deliberate, documented exception to "AI never inline" (this is the user's primary action, not a side-effect of a write). |
| AI gating | New `Ai:RecipeTagSuggester:Enabled` (+ `:Model`), needs `Ai:ApiKey` too; off by default. `Null` suggester returns `[]` when disabled. |
| Suggest button when AI off | **Always shown**; returns `[]` → "no suggestions". `ponytail:` simplification — no frontend capability flag today. Revisit if it matters. |
| Overview filter combine | Search **AND** all selected tags (recipe must contain every selected tag). Client-side, alongside `rankRecipes`. |
| Facet grouping | UI-only (chips grouped under Course / Dietary headers); derived from two TS const arrays of tag names. |

## Taxonomy — `RecipeTag`

Flat enum, string-name on the wire (like every other enum), int-backed in the
DB. Course occupies the low range, Dietary a higher range, so the numeric value
itself groups the facet; no member at 0 (a recipe with no fitting tag simply has
an empty set).

```csharp
public enum RecipeTag
{
    // Course (1–19)
    Breakfast = 1,
    Starter = 2,
    Main = 3,
    Side = 4,
    Salad = 5,
    Soup = 6,
    Dessert = 7,
    Snack = 8,
    Drink = 9,
    Sauce = 10,
    Baking = 11,
    Bread = 12,

    // Dietary (20+)
    Vegetarian = 20,
    Vegan = 21,
    GlutenFree = 22,
    DairyFree = 23,
    LactoseFree = 24,
    LowCarb = 25,
}
```

Notes:
- `DairyFree` and `LactoseFree` are deliberately distinct (lactose-free dairy
  still contains milk proteins; dairy-free excludes all dairy).
- The suggester's strict-output schema derives its enum from
  `Enum.GetNames<RecipeTag>()`, so adding/removing a value updates the schema
  with no hand-edit — but the `OpenAiRecipeTagSuggester` system prompt must
  describe each value (one short line).
- Reserving numeric ranges per facet is cosmetic (keeps the facet readable in raw
  data); the frontend grouping uses explicit TS arrays, not the numeric range.

## Domain (`Frigorino.Domain/Entities/Recipe.cs`)

- New property `public List<RecipeTag> Tags { get; set; } = [];` — a value-set,
  not an aggregate child (no `Id`, `Rank`, `IsActive`).
- New constant `public const int MaxTags = 10;`.
- New aggregate method:

```csharp
public Result SetTags(string callerUserId, HouseholdRole callerRole, IEnumerable<RecipeTag> tags)
```

Rules:
- `CanBeManagedBy(callerUserId, callerRole)` → else `AccessDeniedError`.
- Distinct the input; reject unknown enum values
  (`!Enum.IsDefined`) → `Error` with `Property = nameof(Tags)`.
- Reject count > `MaxTags` → `Error` with `Property = nameof(Tags)`.
- Replace `Tags` wholesale (multi-select semantics — empty list clears all),
  bump `UpdatedAt`.

No change to `Recipe.Create` — recipes start with no tags. No chaining to any
other pipeline.

## Storage / EF (`Frigorino.Infrastructure/EntityFramework/`)

- Map `Recipe.Tags` to a PostgreSQL `integer[]` column (Npgsql primitive
  collection of the int-backed enum). One migration
  (`AddRecipeTags`) adding a non-null `integer[]` column defaulting to `{}`.
- No index needed (filtering is client-side). If a future server-side filter
  arrives, a GIN index is the additive next step (note only).
- Fallback if the enum-array mapping proves fiddly: a value converter to a
  delimited `text` column. Same single column, no schema reshape, decided at
  implementation time and recorded if used.

## Backend slices (`Frigorino.Features/Recipes/`)

### `Tags/SetRecipeTags.cs` — `PUT /api/household/{householdId}/recipes/{recipeId}/tags`

- Request DTO: `{ RecipeTag[] Tags }`.
- Standard household-membership check, load recipe (active), call
  `recipe.SetTags(currentUser.UserId, membership.Role, body.Tags)`, save.
- Error dispatch per the slice convention (`EntityNotFoundError` → 404,
  `AccessDeniedError` → 403, generic `Error` → `ValidationProblem`).
- Returns the updated `RecipeResponse` (or 204 — match the sibling
  `UpdateRecipe` slice's convention).

### `Tags/SuggestRecipeTags.cs` — `POST /api/household/{householdId}/recipes/{recipeId}/suggest-tags`

- No request body. Membership check + load recipe (active) with its active items.
- Call `IRecipeTagSuggester.SuggestAsync(recipe.Name, recipe.Description,
  ingredients, ct)` **synchronously** and return
  `{ RecipeTag[] SuggestedTags }`.
- Deliberate inline-AI exception — documented in `AI_Classification.md`. Disabled
  path returns `[]` (the `Null` suggester), so the endpoint is always safe to
  call.

### `RecipeResponse` (`Recipes/RecipeResponse.cs`)

- Add `IReadOnlyList<RecipeTag> Tags`. `ToProjection` reads `r.Tags`; `From`
  defaults `[]` (same pattern as `Ingredients` / `CoverAttachmentId`).
- Run `npm run api` from `ClientApp/` and commit the regenerated client.

### Wiring (`Frigorino.Web/Program.cs`)

- Register `recipes.MapSetRecipeTags()` and a recipe-scoped
  `MapSuggestRecipeTags()` in the existing recipes group.

## Infrastructure — AI suggester (`Frigorino.Infrastructure/Services/`)

Mirror the existing classifier/extractor exactly:

- Port `IRecipeTagSuggester` in `Frigorino.Domain/Interfaces/`:
  `Task<IReadOnlyList<RecipeTag>> SuggestAsync(string name, string? description,
  IReadOnlyList<string> ingredients, CancellationToken ct)`.
- `OpenAiRecipeTagSuggester` — OpenAI Structured Outputs, strict schema whose
  allowed values come from `Enum.GetNames<RecipeTag>()`; refusals/empties map to
  an empty list (a valid "no confident tags" answer, not an error); reasoning
  logged for diagnostics only, never persisted.
- `NullRecipeTagSuggester` — returns `[]`.
- `AddRecipeTagSuggestion` DI extension, called from `Program.cs`: registers a
  keyed OpenAI `ChatClient` + the real suggester **only** when `Ai:ApiKey` **and**
  `Ai:RecipeTagSuggester:Enabled` are set; otherwise the `Null` suggester.
- Config block (`appsettings.json` placeholder + Railway env):
  `Ai:RecipeTagSuggester:Enabled` (default off), `Ai:RecipeTagSuggester:Model`
  (default a mini/nano model, e.g. `gpt-5.4-nano`).

No backfill task (suggestions are on-demand, nothing to backfill).

## Frontend (`ClientApp/src/features/recipes/`)

### Tag vocabulary helper

A small module (e.g. `features/recipes/tags.ts`) exporting `COURSE_TAGS` and
`DIETARY_TAGS` as ordered arrays of the generated `RecipeTag` string-union
values, plus a `tagLabel(tag)` that maps each to its i18n key. Single source for
grouping + ordering on every surface.

### Hooks (one-per-file, generated-options spread)

- `useSetRecipeTags` — arg-less mutation; caller passes `{ path, body: { tags } }`.
  `onSuccess`/`onSettled` invalidate the recipe + the household recipe list via
  `getXQueryKey`.
- `useSuggestRecipeTags` — arg-less mutation calling the suggest endpoint; returns
  the suggested tags to the component (no cache write).

### Edit page

- Grouped multi-select tag chips (Course / Dietary subheaders) reflecting
  `recipe.tags`; toggling a chip calls `useSetRecipeTags` with the new full set.
- A **"Suggest tags"** button: on tap, `useSuggestRecipeTags` runs (button shows a
  spinner / disabled while pending). Returned tags **not already selected** render
  as **ghost/outlined chips** beside the selected set; tapping a ghost chip adds
  it to the selected set (a normal `useSetRecipeTags` write). Ghost chips are
  local component state — they vanish on navigate/close. Nothing persisted.

### View page

- Render `recipe.tags` as read-only chips (grouped or simply ordered Course then
  Dietary).

### Overview (`pages/RecipesPage.tsx`)

- A tag-filter chip row above the list (grouped Course / Dietary), each chip
  toggling membership in a local `selectedTags` set.
- Filtering stays client-side: a recipe is visible when it passes `rankRecipes`
  for the query **and** its `tags` contain **every** `selectedTags` entry (AND).
  Keep `rankRecipes` as the search/sort core; apply the tag predicate as a filter
  step (in the page `useMemo` or a thin wrapper) so search ranking is unaffected.
- Empty `selectedTags` → no tag filtering (today's behavior).

### i18n (en + de)

New keys under the existing `recipes` namespace (JSON only — existing namespace,
no `i18next.d.ts` change): one label per `RecipeTag` value, the two facet
headers, "Suggest tags", a "no suggestions" message, and a filter-row
label/empty-filter message as needed.

### Testids

Stable testids for IT: per-tag selectable chip (`recipe-tag-<Tag>`), the suggest
button (`recipe-suggest-tags`), suggested ghost chip (`recipe-tag-suggested-<Tag>`),
and overview filter chips (`recipe-filter-tag-<Tag>`). Assert on testids /
`data-*`, never translated text.

## Testing

- **Unit (`Frigorino.Test`):** `Recipe.SetTags` — happy path (replace set),
  de-dupe, unknown value rejected, over-cap rejected, access denied for a
  non-manager.
- **Integration (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Postgres
  Testcontainers):**
  1. Set tags via the edit page → reload → tags persist and show on the view page.
  2. Overview filter — selecting a tag chip narrows the list to recipes carrying
     it; combined with a search query, both constraints apply (AND).
  3. Suggest button (with AI enabled in the test via a **fake**
     `IRecipeTagSuggester` returning a known set) → ghost chips appear → tapping
     one adds it to the selected set and it persists. No real OpenAI call in tests.
- Reminder: the IT harness serves the SPA from `ClientApp/build`; run
  `npm run build` after the React changes or the new testids won't appear.

## Verification

- Frontend: `npm run lint`, `npm run tsc`, `npm run prettier`, `npm run api`
  (regen + commit client).
- Backend/E2E: `dotnet test Application/Frigorino.sln` (unit + integration).
- Final gate: `docker build -f Application/Dockerfile -t frigorino .` (catches
  pipeline/SPA/Dockerfile drift; also confirm the new csproj refs / DI wiring
  build clean).

## Documentation

In the same change, update:
- `knowledge/Recipes.md` — tags on the domain table, the two new slices, the
  overview filter, and the edit/view tag UI.
- `knowledge/AI_Classification.md` — the new on-demand suggester (synchronous,
  stateless, suggest-only), its gating/config block, and the chaining-summary row
  (recipe tag suggestion → nothing).
- `CLAUDE.md` — add `AddRecipeTagSuggestion` to the DI extension list and the
  `Ai:RecipeTagSuggester:*` config keys.

## Branching note

This feature builds on the overview-search hub (it extends
`RecipeResponse.ToProjection` and the overview's `rankRecipes`/`RecipesPage`).
That work squash-merged into `stage` as `99b39dd` ("feat(recipes): overview
search hub"), so `feat/recipes-tags` is cut off `stage` per the normal rule —
the foundation is present.
