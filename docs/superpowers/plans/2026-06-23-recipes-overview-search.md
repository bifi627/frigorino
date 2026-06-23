# Recipes Overview Search Hub — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the recipes overview into a scannable search hub — live name/description/ingredient search with relevance ranking, denser cover-thumbnail cards, and an inline "peek" before opening a recipe.

**Architecture:** One additive backend projection change (two fields on `RecipeResponse` — `coverAttachmentId`, `ingredients`); no new endpoint, no migration. Everything else is frontend: a client-side ranking util, a reworked `RecipeSummaryCard` (one-line card + cover thumb + chevron-expanded peek), and a search field on `RecipesPage`. Behavior is verified end-to-end through the existing Reqnroll + Playwright integration suite (no JS unit runner exists in this repo).

**Tech Stack:** .NET 10 vertical slices + EF Core (Postgres) backend; React 19 + TanStack Router/Query + MUI frontend; hey-api generated client; Reqnroll + Playwright + Postgres Testcontainers for integration tests.

**Spec:** `docs/superpowers/specs/2026-06-23-recipes-overview-search-design.md`

## Global Constraints

Every task implicitly includes these (verbatim from the spec + repo conventions):

- **C# brace style:** always block braces `{ }`, even single-line conditions and namespaces.
- **No new dependencies.** Reuse what's installed. NuGet pinned exact, npm caret-minor — but nothing new is needed here.
- **Enums serialize as string names** on the wire (already configured globally) — not relevant to the new fields but don't fight it.
- **Frontend tooling only via npm scripts:** `npm run lint`, `npm run tsc`, `npm run prettier`, `npm run build`, `npm run api` — never raw `npx eslint/tsc/prettier`.
- **API client is generated:** after any backend DTO change, run `npm run api` from `ClientApp/` and commit the regenerated `src/lib/api`. Never hand-edit generated code.
- **Hooks follow the one-hook-per-file convention** — spread generated `getXOptions`/`xMutation`; never write `queryFn`/`mutationFn`/manual `queryKey`. (No new hooks are required by this plan; reuse `useHouseholdRecipes` + `useAttachmentImage`.)
- **Test assertions use testids / `data-*` attributes, never translated text.** Use retrying `Expect(...).ToBeVisibleAsync()` / `.Not.ToBeVisibleAsync()`, never snapshot `IsVisibleAsync`.
- **The IT harness serves the SPA from `ClientApp/build`** — run `npm run build` after any React change before running integration tests, or new testids won't appear.
- **Reqnroll step text must be globally unique** across the whole IT assembly and is **keyword-sensitive** (Given/When/Then must match). Reqnroll's `--filter "FullyQualifiedName~X"` builds the FQN from the sanitized scenario *title*, not the file name — when filtering, confirm `Gesamt: N` (German "total") matches the scenarios you intended to run, or just run the whole IT project.
- **i18n:** new keys go in **both** `en` and `de` `translation.json`. These all live under the existing `recipes` namespace, so no `src/types/i18next.d.ts` change is needed.
- **Styling:** use the theme — MUI size props, `<Card>`/`<Paper>`, no inline `borderRadius: 2` / manual `boxShadow` / `fontSize: { xs, sm }`.

---

## File Structure

**Backend (one file + regenerated client):**
- Modify `Application/Frigorino.Features/Recipes/RecipeResponse.cs` — add `CoverAttachmentId` + `Ingredients` to the record, `ToProjection`, and `From`.
- Regenerated: `Application/Frigorino.Web/ClientApp/src/lib/api/**` (via `npm run api`).

**Frontend:**
- Create `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeCoverThumb.tsx` — cover thumbnail (image or placeholder).
- Create `Application/Frigorino.Web/ClientApp/src/features/recipes/searchRecipes.ts` — pure ranking util.
- Modify `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeSummaryCard.tsx` — one-line card + chevron peek; relocates the ⋮ menu into the peek (fixes the overlap bug).
- Modify `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx` — search field + expand state + ranked render.
- Modify `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `de/translation.json` — 3 new keys.

**Tests + docs:**
- Modify `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature` + `RecipeApiSteps.cs` — projection coverage.
- Modify `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature` + `RecipeSteps.cs` — search/ranking/peek coverage + fix the delete step.
- Modify `knowledge/Recipes.md` — document the new overview behavior.

---

## Task 1: Backend projection — `coverAttachmentId` + `ingredients`

**Files:**
- Modify: `Application/Frigorino.Features/Recipes/RecipeResponse.cs`
- Test: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature` (add scenarios)
- Test: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeApiSteps.cs` (add steps)
- Regenerate: `Application/Frigorino.Web/ClientApp/src/lib/api/**` (via `npm run api`)

**Interfaces:**
- Produces: `RecipeResponse` gains `int? CoverAttachmentId` and `IReadOnlyList<string> Ingredients` (positional record params, in that order, appended after `int ItemCount`). The generated TS type `RecipeResponse` gains `coverAttachmentId?: number | null` and `ingredients: Array<string>`. The list endpoint `GET /api/household/{householdId}/recipes` returns them per entry. Tasks 2 and 3 consume these.

- [ ] **Step 1: Write the failing API scenarios**

Add to the end of `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature`:

```gherkin
  Scenario: Recipe list includes ingredient names and a null cover for an image-less recipe
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour"
    And the recipe "Pancakes" has an item "Eggs"
    When I GET the recipes of the active household via the API
    Then the API response status is 200
    And the API recipe list entry "Pancakes" has ingredients "Flour, Eggs"
    And the API recipe list entry "Pancakes" has no cover attachment

  Scenario: Recipe list includes the cover attachment id when the recipe has an image
    Given there is a recipe named "PhotoCake"
    And the recipe "PhotoCake" has an image attachment
    When I GET the recipes of the active household via the API
    Then the API response status is 200
    And the API recipe list entry "PhotoCake" has a cover attachment
```

> Note: `the recipe {string} has an item {string}` is an existing seed step (used elsewhere in this feature). The other phrases are new and added in Step 2.

- [ ] **Step 2: Add the new step bindings**

Add to `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeApiSteps.cs`. First add `using System.Text.Json;` at the top (alongside the existing usings), then add these members to the `RecipeApiSteps` class:

```csharp
    [Given("the recipe {string} has an image attachment")]
    public async Task GivenTheRecipeHasAnImageAttachment(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        await api.CreateRecipeImageAttachmentAsync(recipeId);
    }

    [When("I GET the recipes of the active household via the API")]
    public async Task WhenIGetTheRecipesOfTheActiveHouseholdViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetRecipesAsync();
    }

    [Then("the API recipe list entry {string} has ingredients {string}")]
    public async Task ThenTheApiRecipeListEntryHasIngredients(string recipeName, string csv)
    {
        var entry = await FindRecipeListEntryAsync(recipeName);
        var actual = entry.GetProperty("ingredients").EnumerateArray()
            .Select(e => e.GetString()).ToArray();
        var expected = csv.Split(',').Select(s => s.Trim()).ToArray();
        Assert.Equal(expected, actual);
    }

    [Then("the API recipe list entry {string} has no cover attachment")]
    public async Task ThenTheApiRecipeListEntryHasNoCoverAttachment(string recipeName)
    {
        var entry = await FindRecipeListEntryAsync(recipeName);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("coverAttachmentId").ValueKind);
    }

    [Then("the API recipe list entry {string} has a cover attachment")]
    public async Task ThenTheApiRecipeListEntryHasACoverAttachment(string recipeName)
    {
        var entry = await FindRecipeListEntryAsync(recipeName);
        Assert.Equal(JsonValueKind.Number, entry.GetProperty("coverAttachmentId").ValueKind);
    }

    private async Task<JsonElement> FindRecipeListEntryAsync(string recipeName)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        foreach (var entry in json.EnumerateArray())
        {
            if (entry.GetProperty("name").GetString() == recipeName)
            {
                return entry.Clone();
            }
        }
        throw new Xunit.Sdk.XunitException($"Recipe '{recipeName}' not found in list response");
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run (from repo root):
```bash
dotnet test Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj --filter "FullyQualifiedName~ListIncludes"
```
Expected: FAIL — the GET response has no `ingredients`/`coverAttachmentId` properties yet, so `GetProperty("ingredients")` throws `KeyNotFoundException`. Confirm `Gesamt: 2` (both new scenarios ran).

- [ ] **Step 4: Implement the projection**

Replace the body of `Application/Frigorino.Features/Recipes/RecipeResponse.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes
{
    public sealed record RecipeResponse(
        int Id,
        string Name,
        string? Description,
        int? Servings,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        RecipeCreatorResponse CreatedByUser,
        int ItemCount,
        int? CoverAttachmentId,
        IReadOnlyList<string> Ingredients)
    {
        // CoverAttachmentId + Ingredients are populated authoritatively by ToProjection (the list
        // endpoint, their only consumer). Create/Update load neither attachments nor items, and their
        // callers don't read these fields, so From returns the empty defaults.
        // ponytail: the spec chose to reuse this DTO across both GET slices over a list-only DTO.
        public static RecipeResponse From(Recipe recipe, User creator, int itemCount)
            => new(recipe.Id, recipe.Name, recipe.Description, recipe.Servings, recipe.HouseholdId,
                   recipe.CreatedAt, recipe.UpdatedAt,
                   new RecipeCreatorResponse(creator.ExternalId, creator.Name, creator.Email), itemCount,
                   CoverAttachmentId: null,
                   Ingredients: []);

        public static readonly Expression<Func<Recipe, RecipeResponse>> ToProjection = r => new RecipeResponse(
            r.Id, r.Name, r.Description, r.Servings, r.HouseholdId, r.CreatedAt, r.UpdatedAt,
            new RecipeCreatorResponse(r.CreatedByUser.ExternalId, r.CreatedByUser.Name, r.CreatedByUser.Email),
            r.Items.Count(x => x.IsActive),
            r.Attachments
                .Where(a => a.IsActive && a.Type == AttachmentType.Image)
                .OrderBy(a => a.Rank)
                .Select(a => (int?)a.Id)
                .FirstOrDefault(),
            r.Items
                .Where(i => i.IsActive)
                .OrderBy(i => i.Section.Rank)
                .ThenBy(i => i.Rank)
                .Select(i => i.Text)
                .ToList());
    }

    public sealed record RecipeCreatorResponse(string ExternalId, string Name, string? Email);
}
```

This is EF-translatable: `Attachments` and `Items` are real navigations, `AttachmentType.Image` is a constant, and ordering by `Section.Rank` then `Rank` projects to SQL. `From`'s call sites in `CreateRecipe.cs`/`UpdateRecipe.cs` pass through unchanged (they only supply the first three args).

- [ ] **Step 5: Run the tests to verify they pass**

Run:
```bash
dotnet test Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj --filter "FullyQualifiedName~ListIncludes"
```
Expected: PASS, `Gesamt: 2`. (Testcontainers spins up a real Postgres — first run is slow.)

- [ ] **Step 6: Regenerate the API client**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run api
```
Expected: rebuilds the backend, emits `src/lib/openapi.json`, regenerates `src/lib/api`. Confirm `src/lib/api/types.gen.ts` now shows `coverAttachmentId?: number | null` and `ingredients: Array<string>` on `RecipeResponse`.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/Recipes/RecipeResponse.cs \
        Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature \
        Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeApiSteps.cs \
        Application/Frigorino.Web/ClientApp/src/lib
git commit -m "feat(recipes): project cover attachment id + ingredients into recipe list"
```

---

## Task 2: Reworked card + inline peek (fixes the overlap bug)

Rework `RecipeSummaryCard` into a one-line card with a cover thumbnail and a chevron that expands an inline peek (full description + ingredient chips + Open + ⋮). The ⋮ menu moves into the peek, which is what frees the collapsed row of the absolutely-positioned `secondaryAction` that overlaps the description today. The existing "delete from card menu" scenario must be updated to expand first.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeCoverThumb.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeSummaryCard.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `de/translation.json`
- Test: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature` (add peek scenario)
- Test: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs` (add steps + fix the delete step)

**Interfaces:**
- Consumes: `RecipeResponse.coverAttachmentId`, `RecipeResponse.ingredients` (Task 1); `useAttachmentImage(householdId, recipeId, attachmentId, "thumbnail", enabled)` (existing).
- Produces: `RecipeSummaryCard` props `{ recipe, householdId, expanded, query, onToggleExpand(recipeId), onOpen(recipeId), onMenuOpen(event, recipe) }`. New testids: card root `recipe-card-{name}` (+ `data-recipe-name="{name}"`), chevron `recipe-card-toggle-{name}`, peek region `recipe-card-peek-{name}`, open button `recipe-open-button-{name}`. Preserved testids: open target `recipe-item-{name}`, count `recipe-item-count-{name}`, menu button `recipe-item-menu-button-{name}`.

- [ ] **Step 1: Add i18n key `openRecipe` (en + de)**

In `public/locales/en/translation.json`, inside the `recipes` object, after the `"untitledRecipe"` line add:
```json
        "openRecipe": "Open recipe",
```
In `public/locales/de/translation.json`, in the same place:
```json
        "openRecipe": "Rezept öffnen",
```
(The peek's ingredients label reuses the existing `recipes.ingredientsHeading` = "Ingredients"/"Zutaten"; the count chip reuses `recipes.recipeItemCount_one/_other`.)

- [ ] **Step 2: Write the failing peek scenario + fix the delete step**

Add to `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature` (after the existing delete scenario):

```gherkin
  Scenario: Expanding a recipe card peeks its ingredients without leaving the overview
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour"
    When I navigate to "/recipes"
    And I expand the recipe card "Pancakes"
    Then the recipe card peek for "Pancakes" shows "Flour"
    When I open the peeked recipe "Pancakes"
    Then I am on the recipe view page for "Pancakes"
```

In `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs`, **replace** the existing `WhenIOpenTheRecipeCardMenuFor` method (the ⋮ button now lives inside the peek, so expand first):

```csharp
    [When("I open the recipe card menu for {string}")]
    public async Task WhenIOpenTheRecipeCardMenuFor(string recipeName)
    {
        // The ⋮ menu lives inside the card's expanded peek — open the peek first.
        await ctx.Page.GetByTestId($"recipe-card-toggle-{recipeName}").ClickAsync();
        await ctx.Page.GetByTestId($"recipe-item-menu-button-{recipeName}").ClickAsync();
    }
```

And add these new steps to the same class:

```csharp
    [When("I expand the recipe card {string}")]
    public async Task WhenIExpandTheRecipeCard(string recipeName)
    {
        await ctx.Page.GetByTestId($"recipe-card-toggle-{recipeName}").ClickAsync();
    }

    [Then("the recipe card peek for {string} shows {string}")]
    public async Task ThenTheRecipeCardPeekShows(string recipeName, string text)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-card-peek-{recipeName}"))
            .ToContainTextAsync(text);
    }

    [When("I open the peeked recipe {string}")]
    public async Task WhenIOpenThePeekedRecipe(string recipeName)
    {
        await ctx.Page.GetByTestId($"recipe-open-button-{recipeName}").ClickAsync();
    }
```

> `there is a recipe named {string}`, `the recipe {string} has an item {string}`, `I navigate to {string}`, and `I am on the recipe view page for {string}` are existing steps — reuse, don't redefine (Reqnroll step text is globally unique).

- [ ] **Step 3: Run the scenario to verify it fails**

First build is required because the IT serves `ClientApp/build`:
```bash
cd Application/Frigorino.Web/ClientApp && npm run build && cd -
dotnet test Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj --filter "FullyQualifiedName~Peeks"
```
Expected: FAIL — there is no `recipe-card-toggle-Pancakes` element yet. Confirm `Gesamt: 1`.

- [ ] **Step 4: Create `RecipeCoverThumb.tsx`**

```tsx
import { RestaurantMenu } from "@mui/icons-material";
import { Box } from "@mui/material";
import { useAttachmentImage } from "../attachments/useAttachmentImage";

interface RecipeCoverThumbProps {
    householdId: number;
    recipeId: number;
    coverAttachmentId?: number | null;
    name: string;
}

const SIZE = 52;

export const RecipeCoverThumb = ({
    householdId,
    recipeId,
    coverAttachmentId,
    name,
}: RecipeCoverThumbProps) => {
    const hasCover = Boolean(coverAttachmentId && coverAttachmentId > 0);
    const { data: url } = useAttachmentImage(
        householdId,
        recipeId,
        coverAttachmentId ?? 0,
        "thumbnail",
        hasCover,
    );

    return (
        <Box
            sx={{
                width: SIZE,
                height: SIZE,
                flexShrink: 0,
                borderRadius: 1.5,
                overflow: "hidden",
                bgcolor: "action.hover",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "text.disabled",
            }}
        >
            {url ? (
                <Box
                    component="img"
                    src={url}
                    alt={name}
                    sx={{ width: "100%", height: "100%", objectFit: "cover" }}
                />
            ) : (
                <RestaurantMenu fontSize="small" />
            )}
        </Box>
    );
};
```

- [ ] **Step 5: Rewrite `RecipeSummaryCard.tsx`**

Replace the whole file with:

```tsx
import { ExpandLess, ExpandMore, MoreVert } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    Chip,
    Collapse,
    IconButton,
    Stack,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { RecipeCoverThumb } from "./RecipeCoverThumb";

interface RecipeSummaryCardProps {
    recipe: RecipeResponse;
    householdId: number;
    expanded: boolean;
    query: string;
    onToggleExpand: (recipeId: number) => void;
    onOpen: (recipeId: number) => void;
    onMenuOpen: (
        event: React.MouseEvent<HTMLElement>,
        recipe: RecipeResponse,
    ) => void;
}

const MAX_PEEK_CHIPS = 8;
const oneLineSx = {
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
} as const;

export const RecipeSummaryCard = ({
    recipe,
    householdId,
    expanded,
    query,
    onToggleExpand,
    onOpen,
    onMenuOpen,
}: RecipeSummaryCardProps) => {
    const { t } = useTranslation();
    const q = query.trim().toLowerCase();
    const ingredients = recipe.ingredients ?? [];
    const shownIngredients = ingredients.slice(0, MAX_PEEK_CHIPS);
    const overflowCount = ingredients.length - shownIngredients.length;

    const open = () => recipe.id && onOpen(recipe.id);
    const toggle = (e: React.MouseEvent<HTMLElement>) => {
        e.stopPropagation();
        if (recipe.id) {
            onToggleExpand(recipe.id);
        }
    };

    return (
        <Card
            elevation={1}
            data-testid={`recipe-card-${recipe.name}`}
            data-recipe-name={recipe.name}
        >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, p: 1 }}>
                <Box
                    data-testid={`recipe-item-${recipe.name}`}
                    onClick={open}
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: 1.5,
                        flex: 1,
                        minWidth: 0,
                        cursor: "pointer",
                    }}
                >
                    <RecipeCoverThumb
                        householdId={householdId}
                        recipeId={recipe.id ?? 0}
                        coverAttachmentId={recipe.coverAttachmentId}
                        name={recipe.name}
                    />
                    <Box sx={{ minWidth: 0 }}>
                        <Typography
                            variant="body1"
                            sx={{ fontWeight: 600, ...oneLineSx }}
                        >
                            {recipe.name || t("recipes.untitledRecipe")}
                        </Typography>
                        {recipe.description && (
                            <Typography
                                variant="body2"
                                sx={{ color: "text.secondary", ...oneLineSx }}
                            >
                                {recipe.description}
                            </Typography>
                        )}
                    </Box>
                </Box>
                <Chip
                    label={t("recipes.recipeItemCount", {
                        count: recipe.itemCount,
                        defaultValue: `${recipe.itemCount} items`,
                    })}
                    size="small"
                    variant="outlined"
                    data-testid={`recipe-item-count-${recipe.name}`}
                />
                <IconButton
                    size="small"
                    aria-expanded={expanded}
                    data-testid={`recipe-card-toggle-${recipe.name}`}
                    onClick={toggle}
                >
                    {expanded ? (
                        <ExpandLess fontSize="small" />
                    ) : (
                        <ExpandMore fontSize="small" />
                    )}
                </IconButton>
            </Box>

            <Collapse in={expanded} unmountOnExit>
                <Box
                    data-testid={`recipe-card-peek-${recipe.name}`}
                    sx={{
                        px: 1,
                        pb: 1.5,
                        pt: 1,
                        borderTop: 1,
                        borderColor: "divider",
                    }}
                >
                    {recipe.description && (
                        <Typography
                            variant="body2"
                            sx={{ color: "text.secondary", mb: 1.5 }}
                        >
                            {recipe.description}
                        </Typography>
                    )}
                    {ingredients.length > 0 && (
                        <>
                            <Typography
                                variant="overline"
                                sx={{ color: "primary.main", fontWeight: 700 }}
                            >
                                {t("recipes.ingredientsHeading")}
                            </Typography>
                            <Box
                                sx={{
                                    display: "flex",
                                    flexWrap: "wrap",
                                    gap: 0.5,
                                    mb: 1.5,
                                }}
                            >
                                {shownIngredients.map((text, i) => {
                                    const isHit =
                                        q.length > 0 &&
                                        text.toLowerCase().includes(q);
                                    return (
                                        <Chip
                                            key={`${text}-${i}`}
                                            label={text}
                                            size="small"
                                            color={isHit ? "primary" : "default"}
                                            variant={
                                                isHit ? "filled" : "outlined"
                                            }
                                        />
                                    );
                                })}
                                {overflowCount > 0 && (
                                    <Chip
                                        label={`+${overflowCount}`}
                                        size="small"
                                        variant="outlined"
                                    />
                                )}
                            </Box>
                        </>
                    )}
                    <Stack direction="row" spacing={1} alignItems="center">
                        <Button
                            variant="contained"
                            size="small"
                            onClick={open}
                            data-testid={`recipe-open-button-${recipe.name}`}
                            sx={{ flex: 1 }}
                        >
                            {t("recipes.openRecipe")}
                        </Button>
                        <IconButton
                            size="small"
                            data-testid={`recipe-item-menu-button-${recipe.name}`}
                            onClick={(e) => {
                                e.stopPropagation();
                                onMenuOpen(e, recipe);
                            }}
                        >
                            <MoreVert fontSize="small" />
                        </IconButton>
                    </Stack>
                </Box>
            </Collapse>
        </Card>
    );
};
```

- [ ] **Step 6: Wire the card into `RecipesPage.tsx`**

In `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx`, add expand state and pass the new props. Add to the imports from `react`: `useState` is already imported. Add a state declaration alongside the existing ones:

```tsx
    const [expandedRecipeId, setExpandedRecipeId] = useState<number | null>(
        null,
    );

    const handleToggleExpand = (recipeId: number) =>
        setExpandedRecipeId((current) =>
            current === recipeId ? null : recipeId,
        );
```

Then replace the recipes-list block (the `recipes && recipes.length > 0 && (...)` `Stack`) with:

```tsx
                {recipes && recipes.length > 0 && (
                    <Stack spacing={1.5}>
                        {recipes.map((recipe) => (
                            <RecipeSummaryCard
                                key={recipe.id}
                                recipe={recipe}
                                householdId={householdId}
                                expanded={expandedRecipeId === recipe.id}
                                query=""
                                onToggleExpand={handleToggleExpand}
                                onOpen={handleRecipeClick}
                                onMenuOpen={handleMenuOpen}
                            />
                        ))}
                    </Stack>
                )}
```

(`query=""` is a placeholder for this task — Task 3 replaces it with the real search query. The card's highlight is simply inert while the query is empty.)

- [ ] **Step 7: Type-check, lint, format, build**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run tsc && npm run lint && npm run prettier && npm run build
```
Expected: all pass; `build/` regenerated with the new testids.

- [ ] **Step 8: Run the peek scenario + the delete scenario to verify they pass**

```bash
dotnet test Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj --filter "FullyQualifiedName~RecipesFeature"
```
Expected: PASS — the whole `Recipes` UI feature, including the new "Expanding a recipe card peeks…" scenario and the now-updated "User deletes a recipe from the recipe list" scenario. Confirm `Gesamt` equals the UI feature's scenario count (7 after this task) and none failed.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes \
        Application/Frigorino.Web/ClientApp/public/locales \
        Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature \
        Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs
git commit -m "feat(recipes): one-line cards with cover thumbnail + inline peek"
```

---

## Task 3: Search field + relevance ranking

Add a search field to the overview and a pure ranking util that filters/orders the loaded list by name > description > ingredient match.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/searchRecipes.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `de/translation.json`
- Test: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature` (add search/ranking scenarios)
- Test: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs` (add steps)

**Interfaces:**
- Consumes: `RecipeResponse` (with `ingredients`); `RecipeSummaryCard` `query` prop (Task 2).
- Produces: `rankRecipes(recipes: RecipeResponse[], query: string): RecipeResponse[]`. New testid `recipe-search-input` (the search field). New steps for search + order assertions.

- [ ] **Step 1: Add i18n keys `searchRecipesPlaceholder` + `noRecipeMatches` (en + de)**

In `public/locales/en/translation.json`, in the `recipes` object (next to `openRecipe`):
```json
        "searchRecipesPlaceholder": "Search recipes",
        "noRecipeMatches": "No recipes match your search.",
```
In `public/locales/de/translation.json`:
```json
        "searchRecipesPlaceholder": "Rezepte durchsuchen",
        "noRecipeMatches": "Keine Rezepte gefunden.",
```

> Don't reuse the existing `recipes.searchPlaceholder`/`recipes.noSearchMatches` — those belong to the recipe **view** page's ingredient search ("Search ingredients") and have different copy.

- [ ] **Step 2: Write the failing search/ranking scenarios**

Add to `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature`:

```gherkin
  Scenario: Searching the overview filters recipes by name
    Given there is a recipe named "Pancakes"
    And there is a recipe named "Lasagna"
    When I navigate to "/recipes"
    And I search the recipe overview for "pancake"
    Then the recipe "Pancakes" appears in the recipe overview
    And "Lasagna" no longer appears in the recipe overview

  Scenario: Searching the overview matches an ingredient name
    Given there is a recipe named "Omelette"
    And the recipe "Omelette" has an item "Eggs"
    And there is a recipe named "Toast"
    When I navigate to "/recipes"
    And I search the recipe overview for "egg"
    Then the recipe "Omelette" appears in the recipe overview
    And "Toast" no longer appears in the recipe overview

  Scenario: Name matches rank above ingredient-only matches
    Given there is a recipe named "Egg Salad"
    And there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Eggs"
    When I navigate to "/recipes"
    And I search the recipe overview for "egg"
    Then the recipe overview lists "Egg Salad" before "Pancakes"
```

Add the new steps to `RecipeSteps.cs`:

```csharp
    [When("I search the recipe overview for {string}")]
    public async Task WhenISearchTheRecipeOverviewFor(string query)
    {
        // Client-side filter — no network round-trip; the Then-step's retrying
        // assertion absorbs the React re-render.
        await ctx.Page.GetByTestId("recipe-search-input").FillAsync(query);
    }

    [Then("the recipe {string} appears in the recipe overview")]
    public async Task ThenTheRecipeAppearsInTheRecipeOverview(string recipeName)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-item-{recipeName}"))
            .ToBeVisibleAsync();
    }

    [Then("the recipe overview lists {string} before {string}")]
    public async Task ThenTheRecipeOverviewListsBefore(string first, string second)
    {
        var names = await ctx.Page.Locator("[data-recipe-name]")
            .EvaluateAllAsync<string[]>(
                "els => els.map(e => e.getAttribute('data-recipe-name'))");
        var i1 = Array.IndexOf(names, first);
        var i2 = Array.IndexOf(names, second);
        Assert.True(
            i1 >= 0 && i2 >= 0 && i1 < i2,
            $"Expected '{first}' before '{second}', got: {string.Join(", ", names)}");
    }
```

> `{string} no longer appears in the recipe overview` already exists (the delete scenario) — reuse it.

- [ ] **Step 3: Build + run to verify they fail**

```bash
cd Application/Frigorino.Web/ClientApp && npm run build && cd -
dotnet test Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj --filter "FullyQualifiedName~RecipesFeature"
```
Expected: the three new scenarios FAIL — there's no `recipe-search-input` element yet, so `FillAsync` times out. (The earlier scenarios still pass.)

- [ ] **Step 4: Create the ranking util `searchRecipes.ts`**

```ts
import type { RecipeResponse } from "../../lib/api";

// Tiered relevance: a name hit (3) outranks a description hit (2), which outranks an
// ingredient-only hit (1). Empty query returns the list unchanged (already newest-first
// from the API). Non-matching recipes are dropped when a query is present. Array.sort is
// stable, so ties keep the API order (newest-first) — that is the tiebreak.
export const rankRecipes = (
    recipes: RecipeResponse[],
    query: string,
): RecipeResponse[] => {
    const q = query.trim().toLowerCase();
    if (!q) {
        return recipes;
    }

    const score = (r: RecipeResponse): number => {
        if (r.name?.toLowerCase().includes(q)) {
            return 3;
        }
        if (r.description?.toLowerCase().includes(q)) {
            return 2;
        }
        if (r.ingredients?.some((i) => i.toLowerCase().includes(q))) {
            return 1;
        }
        return 0;
    };

    return recipes
        .map((recipe) => ({ recipe, s: score(recipe) }))
        .filter((x) => x.s > 0)
        .sort((a, b) => b.s - a.s)
        .map((x) => x.recipe);
};
```

- [ ] **Step 5: Add the search field + ranked render to `RecipesPage.tsx`**

Add imports at the top of the file:
```tsx
import { Add, Search } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    InputAdornment,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
```
(Merge with the existing import lists — `Add` already imported; add `Search`; add `InputAdornment` + `TextField`; ensure `useMemo` is imported from `react`.)

Add the import of the util:
```tsx
import { rankRecipes } from "../searchRecipes";
```

Add query state next to `expandedRecipeId`:
```tsx
    const [query, setQuery] = useState("");

    const visibleRecipes = useMemo(
        () => rankRecipes(recipes ?? [], query),
        [recipes, query],
    );
```

Replace the recipes-list block from Task 2 with the searchable version (search field shown once there are any recipes; ranked list; no-results message):

```tsx
                {recipes && recipes.length > 0 && (
                    <>
                        <TextField
                            fullWidth
                            size="small"
                            value={query}
                            onChange={(e) => setQuery(e.target.value)}
                            placeholder={t("recipes.searchRecipesPlaceholder")}
                            slotProps={{
                                input: {
                                    startAdornment: (
                                        <InputAdornment position="start">
                                            <Search fontSize="small" />
                                        </InputAdornment>
                                    ),
                                },
                                htmlInput: {
                                    "data-testid": "recipe-search-input",
                                },
                            }}
                            sx={{ mb: 2 }}
                        />
                        {visibleRecipes.length > 0 ? (
                            <Stack spacing={1.5}>
                                {visibleRecipes.map((recipe) => (
                                    <RecipeSummaryCard
                                        key={recipe.id}
                                        recipe={recipe}
                                        householdId={householdId}
                                        expanded={
                                            expandedRecipeId === recipe.id
                                        }
                                        query={query}
                                        onToggleExpand={handleToggleExpand}
                                        onOpen={handleRecipeClick}
                                        onMenuOpen={handleMenuOpen}
                                    />
                                ))}
                            </Stack>
                        ) : (
                            <Typography
                                sx={{
                                    color: "text.secondary",
                                    textAlign: "center",
                                    py: 4,
                                }}
                            >
                                {t("recipes.noRecipeMatches")}
                            </Typography>
                        )}
                    </>
                )}
```

(This replaces the Task 2 `Stack` block. The card's `query` prop is now the live search text, enabling the matched-ingredient highlight in the peek.)

- [ ] **Step 6: Type-check, lint, format, build**

```bash
cd Application/Frigorino.Web/ClientApp
npm run tsc && npm run lint && npm run prettier && npm run build
cd -
```
Expected: all pass.

- [ ] **Step 7: Run the recipe UI feature to verify all pass**

```bash
dotnet test Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj --filter "FullyQualifiedName~RecipesFeature"
```
Expected: PASS — all `Recipes` UI scenarios (10 after this task). Confirm none failed and `Gesamt` matches.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes \
        Application/Frigorino.Web/ClientApp/public/locales \
        Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature \
        Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs
git commit -m "feat(recipes): overview search with name/description/ingredient ranking"
```

---

## Task 4: Docs + full verification gate

**Files:**
- Modify: `knowledge/Recipes.md`

- [ ] **Step 1: Update the feature doc**

In `knowledge/Recipes.md`, in the **Frontend** section, update the overview description. Find the sentence listing the thin route shells / root hooks and add a paragraph after the "Edit page is a recipe sheet" block (keep the house style — terse, file-pointed):

```markdown
**Overview is a search hub** (`pages/RecipesPage.tsx`): a search field filters the
loaded list live via `searchRecipes.ts` (`rankRecipes` — tiered relevance: name >
description > ingredient match; empty query keeps newest-first). Cards
(`components/RecipeSummaryCard.tsx`) are one-line with a cover thumbnail
(`components/RecipeCoverThumb.tsx`, reusing `useAttachmentImage`); a chevron expands
an inline peek (full description + ingredient chips + Open + the ⋮ menu). The list
endpoint carries `coverAttachmentId` (first active Image attachment) + `ingredients`
for this (`RecipeResponse.ToProjection`). Search is client-side over the full list —
fine at household scale, server-side at hundreds of recipes (`TECH_DEBT.md`).
```

Also update the **API surface** Recipes line to note the list projection now includes `coverAttachmentId` + `ingredients`, if that section enumerates response fields (keep the edit minimal — only if it doesn't drift).

- [ ] **Step 2: Commit the docs**

```bash
git add knowledge/Recipes.md
git commit -m "docs(recipes): document overview search hub"
```

- [ ] **Step 3: Full verification gate**

Run the whole suite + the Docker build (catches pipeline/SPA/Dockerfile drift the unit tests miss):

```bash
cd Application/Frigorino.Web/ClientApp && npm run lint && npm run tsc && npm run prettier && cd -
dotnet test Application/Frigorino.sln
docker build -f Application/Dockerfile -t frigorino .
```
Expected: lint/tsc/prettier clean; full solution test green (unit + integration); Docker build succeeds. If Docker errors with a daemon-unreachable message, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 4: Final holistic review**

Before declaring done, run an opus-model holistic review of the whole diff (`git diff stage...HEAD`) — check the projection translates as intended (no client-eval EF warning), the StrictMode object-URL rule is respected in `RecipeCoverThumb`, no translated-text assertions slipped into the IT steps, and the overlap bug is actually gone. Fix anything found; re-run the affected verification.

---

## Self-Review (completed during planning)

**Spec coverage:**
- Backend `coverAttachmentId` + `ingredients` projection → Task 1. ✓
- Client-side search + tiered ranking, newest-first on empty → Task 3 (`searchRecipes.ts`). ✓
- One-line cover-thumbnail cards + overlap-bug fix → Task 2. ✓
- Chevron inline peek (description + ingredient chips + Open + relocated ⋮), one at a time → Task 2. ✓
- Matched-ingredient highlight while searching → Task 2 card (`query` prop) wired live in Task 3. ✓
- i18n (en + de), existing namespace → Tasks 2 & 3. ✓
- IT scenarios (name filter, ingredient match, ranking order, peek) → Tasks 1–3. ✓
- `npm run build` before IT, `npm run api` after DTO change → Tasks 1–3. ✓
- Docs update + full gate (sln test + docker) → Task 4. ✓
- Out of scope (server-side search/pagination, tags, sort control, copy-to-list in peek) → not implemented; client-side limit logged in `TECH_DEBT.md`. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code. The one `query=""` in Task 2 is an explicit, documented interim value replaced in Task 3 — not a placeholder.

**Type consistency:** `RecipeResponse.coverAttachmentId` / `ingredients` (Task 1) ↔ consumed by `RecipeCoverThumb` / `RecipeSummaryCard` / `rankRecipes` (Tasks 2–3). `rankRecipes(recipes, query)` signature consistent. Testids consistent across components and IT steps (`recipe-card-toggle-{name}`, `recipe-card-peek-{name}`, `recipe-open-button-{name}`, `recipe-search-input`, `data-recipe-name`). Card props (`expanded`, `query`, `onToggleExpand`, `onOpen`, `onMenuOpen`, `householdId`) match the `RecipesPage` call site.
