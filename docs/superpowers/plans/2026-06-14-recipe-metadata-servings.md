# Recipe Metadata: Servings + Description Editing + Display Scaling — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a numeric `Servings` count to recipes, surface the existing `Description` field as an editable input, and add a display-only quantity-scaling control to the recipe view.

**Architecture:** `Servings` is a nullable int on the `Recipe` aggregate, validated `1..99`, plumbed through the create/update slices, response DTO, EF config, and a migration. `Description` already exists end-to-end and only needs UI inputs. Scaling is a **pure frontend display transform**: the recipe view holds an ephemeral target-servings number, derives a multiplier (`target / base`), and multiplies each ingredient's quantity at render time (rounded to 3 decimals to match the `numeric(12,3)` column). Nothing about scaling is persisted.

**Tech Stack:** .NET 10 vertical slices (FluentResults), EF Core + Postgres, xUnit + FakeItEasy (unit), Reqnroll + Playwright + Testcontainers (integration), React 19 + MUI + TanStack Query/Router, hey-api generated client.

**Spec:** `docs/superpowers/specs/2026-06-14-recipe-metadata-servings-design.md`

---

## File Structure

**Backend (modify):**
- `Application/Frigorino.Domain/Entities/Recipe.cs` — `Servings` property, `ServingsMax` const, validation, threaded into `Create`/`Update`.
- `Application/Frigorino.Features/Recipes/CreateRecipe.cs` — `Servings` on `CreateRecipeRequest`.
- `Application/Frigorino.Features/Recipes/UpdateRecipe.cs` — `Servings` on `UpdateRecipeRequest`.
- `Application/Frigorino.Features/Recipes/RecipeResponse.cs` — `Servings` on the record, `From`, `ToProjection`.
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeConfiguration.cs` — `Servings` property.
- New EF migration (generated).

**Backend (tests):**
- `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs` — servings validation tests.
- `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — create/update-with-servings helpers.
- `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature` — round-trip scenario.
- `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeApiSteps.cs` — steps for the scenario.

**Frontend (modify):**
- `Application/Frigorino.Web/ClientApp/src/lib/api/**` — regenerated client (`npm run api`).
- `src/components/composer/features/quantityFormat.ts` — `scaleQuantity` util.
- `src/components/common/ItemQuantityChip.tsx` — optional `color` prop.
- `src/features/recipes/components/CreateRecipeForm.tsx` — Description + Servings inputs.
- `src/features/recipes/components/EditRecipeForm.tsx` — Description + Servings inputs.
- `src/features/recipes/pages/RecipeViewPage.tsx` — servings stepper + multiplier.
- `src/features/recipes/items/components/RecipeContainer.tsx` — pass `multiplier` through.
- `src/features/recipes/items/components/RecipeItemContent.tsx` — apply scaling + struck-through original.
- `public/locales/en/translation.json` + `public/locales/de/translation.json` — new keys.

---

## Task 1: Domain — `Servings` on the `Recipe` aggregate

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Recipe.cs`
- Test: `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these tests to `RecipeAggregateTests.cs` (inside the class, after `Create_BlankName_Fails`):

```csharp
[Fact]
public void Create_ValidServings_IsStored()
{
    var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: 4);
    Assert.True(result.IsSuccess);
    Assert.Equal(4, result.Value.Servings);
}

[Fact]
public void Create_NullServings_IsAllowed()
{
    var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: null);
    Assert.True(result.IsSuccess);
    Assert.Null(result.Value.Servings);
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(Recipe.ServingsMax + 1)]
public void Create_OutOfRangeServings_FailsWithServingsProperty(int servings)
{
    var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: servings);
    Assert.True(result.IsFailed);
    Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Servings));
}

[Fact]
public void Update_ValidServings_IsStored()
{
    var recipe = NewRecipe();
    var result = recipe.Update("u1", HouseholdRole.Owner, "Apple Pie", null, servings: 6);
    Assert.True(result.IsSuccess);
    Assert.Equal(6, recipe.Servings);
}

[Fact]
public void Update_OutOfRangeServings_FailsWithServingsProperty()
{
    var recipe = NewRecipe();
    var result = recipe.Update("u1", HouseholdRole.Owner, "Apple Pie", null, servings: 0);
    Assert.True(result.IsFailed);
    Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Servings));
}
```

- [ ] **Step 2: Run tests to verify they fail (compile error — signatures don't exist yet)**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests"`
Expected: FAIL — build errors (`Servings`, `ServingsMax`, and the new `Create`/`Update` overloads don't exist).

- [ ] **Step 3: Implement on `Recipe.cs`**

Add the constant next to the existing ones (after `DescriptionMaxLength`):

```csharp
        public const int ServingsMax = 99;
```

Add the property after `Description`:

```csharp
        public int? Servings { get; set; }
```

Change the `Create` signature and body to accept and set `servings` (replace the existing method header + the validation call + the object initializer):

```csharp
        public static Result<Recipe> Create(string name, string? description, int householdId, string createdByUserId, int? servings = null)
        {
            var errors = ValidateMetadata(name, description, servings);
```

…and add `Servings = servings,` to the returned object initializer (after `Description = ...`):

```csharp
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Servings = servings,
```

Change the `Update` signature + validation call + assignment (replace the header, the `ValidateMetadata` call, and add the assignment near `Description = ...`):

```csharp
        public Result Update(string callerUserId, HouseholdRole callerRole, string name, string? description, int? servings = null)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(new AccessDeniedError("Only the recipe creator or an admin can edit this recipe."));
            }

            var errors = ValidateMetadata(name, description, servings);
            if (errors.Count > 0)
            {
                return Result.Fail(errors);
            }

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            Servings = servings;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }
```

Extend `ValidateMetadata` to take and validate `servings` (replace the method header and add the servings check before `return errors;`):

```csharp
        private static List<IError> ValidateMetadata(string name, string? description, int? servings)
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Recipe name is required.").WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Recipe name must be {NameMaxLength} characters or fewer.").WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"Recipe description must be {DescriptionMaxLength} characters or fewer.").WithMetadata("Property", nameof(Description)));
            }
            if (servings is not null && (servings < 1 || servings > ServingsMax))
            {
                errors.Add(new Error($"Servings must be between 1 and {ServingsMax}.").WithMetadata("Property", nameof(Servings)));
            }
            return errors;
        }
```

> Note: `Create` and `Update` keep `servings` as an optional trailing parameter, so the existing `RecipeApiSteps.GivenHasCreatedARecipeNamed` seed (`Recipe.Create(recipeName, null, ...)`) and all current callers still compile unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests"`
Expected: PASS (all existing + 6 new tests green).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Recipe.cs Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs
git commit -m "feat: add validated Servings to Recipe aggregate"
```

---

## Task 2: Slices, response DTO, persistence + migration

**Files:**
- Modify: `Application/Frigorino.Features/Recipes/CreateRecipe.cs:13` (`CreateRecipeRequest`) and the `Recipe.Create` call (`CreateRecipe.cs:41`)
- Modify: `Application/Frigorino.Features/Recipes/UpdateRecipe.cs:14` (`UpdateRecipeRequest`) and the `recipe.Update` call (`UpdateRecipe.cs:47`)
- Modify: `Application/Frigorino.Features/Recipes/RecipeResponse.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeConfiguration.cs:14`

- [ ] **Step 1: Add `Servings` to `CreateRecipeRequest` and pass it through**

In `CreateRecipe.cs`, change the request record:

```csharp
    public sealed record CreateRecipeRequest(string Name, string? Description, int? Servings);
```

And the `Recipe.Create` call:

```csharp
            var creation = Recipe.Create(request.Name, request.Description, householdId, currentUser.UserId, request.Servings);
```

- [ ] **Step 2: Add `Servings` to `UpdateRecipeRequest` and pass it through**

In `UpdateRecipe.cs`, change the request record:

```csharp
    public sealed record UpdateRecipeRequest(string Name, string? Description, int? Servings);
```

And the `recipe.Update` call:

```csharp
            var result = recipe.Update(currentUser.UserId, membership.Role, request.Name, request.Description, request.Servings);
```

- [ ] **Step 3: Add `Servings` to `RecipeResponse`**

Replace the record + `From` + `ToProjection` in `RecipeResponse.cs` with:

```csharp
    public sealed record RecipeResponse(
        int Id,
        string Name,
        string? Description,
        int? Servings,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        RecipeCreatorResponse CreatedByUser,
        int ItemCount)
    {
        public static RecipeResponse From(Recipe recipe, User creator, int itemCount)
            => new(recipe.Id, recipe.Name, recipe.Description, recipe.Servings, recipe.HouseholdId,
                   recipe.CreatedAt, recipe.UpdatedAt,
                   new RecipeCreatorResponse(creator.ExternalId, creator.Name, creator.Email), itemCount);

        public static readonly Expression<Func<Recipe, RecipeResponse>> ToProjection = r => new RecipeResponse(
            r.Id, r.Name, r.Description, r.Servings, r.HouseholdId, r.CreatedAt, r.UpdatedAt,
            new RecipeCreatorResponse(r.CreatedByUser.ExternalId, r.CreatedByUser.Name, r.CreatedByUser.Email),
            r.Items.Count(x => x.IsActive));
    }
```

- [ ] **Step 4: Configure the EF column**

In `RecipeConfiguration.cs`, add after the `Description` line (line 14):

```csharp
            builder.Property(r => r.Servings);
```

(Nullable int needs no extra config — convention maps it to a nullable `integer` column.)

- [ ] **Step 5: Build the backend to verify it compiles**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded (this also regenerates `ClientApp/src/lib/openapi.json` via the MSBuild target — that's expected and gets committed in Task 4).

- [ ] **Step 6: Generate the migration**

Run:
```bash
dotnet ef migrations add AddRecipeServings --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration under `Application/Frigorino.Infrastructure/Migrations/` adding a nullable `Servings` integer column to `Recipes`. Open it and confirm `AddColumn<int>(name: "Servings", ... nullable: true)` in `Up` and a matching `DropColumn` in `Down`, with no unrelated changes.

- [ ] **Step 7: Build again to confirm the migration compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Features/Recipes/ Application/Frigorino.Infrastructure/ Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "feat: plumb Recipe Servings through slices, response, and schema"
```

---

## Task 3: Integration test — servings round-trips through the API

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` (Recipes region, near line 461)
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeApiSteps.cs`

- [ ] **Step 1: Add API client helpers**

In `TestApiClient.cs`, inside the `// ---- Recipes ----` region, add:

```csharp
    public Task<IAPIResponse> TryCreateRecipeWithServingsAsync(string name, int? servings, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes",
            new APIRequestContextOptions
            {
                DataObject = new { name, description = (string?)null, servings },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryUpdateRecipeAsync(int recipeId, string name, int? servings, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}",
            new APIRequestContextOptions
            {
                DataObject = new { name, description = (string?)null, servings },
                Headers = AuthHeaders,
            });
    }
```

- [ ] **Step 2: Write the failing scenario**

Add to `Recipes.Api.feature` (after the empty-name scenario):

```gherkin
  Scenario: Servings round-trips through create and update
    When I POST a recipe named "Banana Bread" with servings 4 via the API
    Then the API response status is 201
    And the API recipe response has servings 4
    When I PUT recipe "Banana Bread" with servings 6 via the API
    Then the API response status is 200
    And the API recipe response has servings 6

  Scenario: Creating a recipe with out-of-range servings returns a validation error
    When I POST a recipe named "Bad" with servings 0 via the API
    Then the API response status is 400
    And the API response has a validation error for "Servings"
```

- [ ] **Step 3: Add the steps**

In `RecipeApiSteps.cs`, add these steps to the class. The create step records the new recipe id into `ctx.RecipeIds` so the PUT step can target it:

```csharp
    [When("I POST a recipe named {string} with servings {int} via the API")]
    public async Task WhenIPostARecipeNamedWithServings(string recipeName, int servings)
    {
        var response = await api.TryCreateRecipeWithServingsAsync(recipeName, servings);
        ctx.LastApiResponse = response;
        if (response.Status == 201)
        {
            var json = (await response.JsonAsync())!.Value;
            ctx.RecipeIds[recipeName] = json.GetProperty("id").GetInt32();
        }
    }

    [When("I PUT recipe {string} with servings {int} via the API")]
    public async Task WhenIPutRecipeWithServings(string recipeName, int servings)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryUpdateRecipeAsync(recipeId, recipeName, servings);
    }

    [Then("the API recipe response has servings {int}")]
    public async Task ThenTheApiRecipeResponseHasServings(int expected)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        Assert.Equal(expected, json.GetProperty("servings").GetInt32());
    }
```

> The `the API response status is N` and `the API response has a validation error for "X"` steps already exist (used by the empty-name scenario) — do not redefine them.

- [ ] **Step 4: Run the new scenarios**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipesAPI"`
Expected: PASS. If the filter matches 0 tests or the wrong set, re-run filtering on scenario-title words (e.g. `~servings`) and confirm the matched count includes both new scenarios before trusting green (Reqnroll sanitizes titles).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/
git commit -m "test: servings round-trips through recipe create/update API"
```

---

## Task 4: Regenerate the frontend API client

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/lib/api/**` (generated)

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run api`
Expected: rebuilds the backend, re-emits `src/lib/openapi.json`, regenerates `src/lib/api/`. `RecipeResponse`, `CreateRecipeRequest`, `UpdateRecipeRequest` types now carry `servings?: number | null`.

- [ ] **Step 2: Verify the generated types include servings**

Run: `git diff --stat Application/Frigorino.Web/ClientApp/src/lib/api/`
Expected: `types.gen.ts` (and possibly request types) changed; the diff mentions `servings`.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/api/ Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "chore: regenerate API client for recipe servings"
```

---

## Task 5: Forms — Description + Servings inputs (Create + Edit)

**Files:**
- Modify: `src/features/recipes/components/CreateRecipeForm.tsx`
- Modify: `src/features/recipes/components/EditRecipeForm.tsx`
- Modify: `public/locales/en/translation.json` + `public/locales/de/translation.json`

- [ ] **Step 1: Add i18n keys**

In `en/translation.json` under `"recipes"`, add:

```json
    "description": "Description",
    "descriptionPlaceholder": "A short note about this recipe",
    "servings": "Servings",
    "servingsFrom": "Servings (from {{count}})",
    "resetServings": "Reset"
```

In `de/translation.json` under `"recipes"`, add:

```json
    "description": "Beschreibung",
    "descriptionPlaceholder": "Eine kurze Notiz zu diesem Rezept",
    "servings": "Portionen",
    "servingsFrom": "Portionen (von {{count}})",
    "resetServings": "Zurücksetzen"
```

- [ ] **Step 2: Add the fields to `CreateRecipeForm.tsx`**

Add two state hooks after `const [name, setName] = useState("");`:

```tsx
    const [description, setDescription] = useState("");
    const [servings, setServings] = useState("");
```

Change the mutation body in `handleSubmit` from the hardcoded `description: null` to:

```tsx
            const response = await createRecipeMutation.mutateAsync({
                path: { householdId },
                body: {
                    name: name.trim(),
                    description: description.trim() || null,
                    servings: servings === "" ? null : Number(servings),
                },
            });
```

Add these fields inside the `<Stack spacing={3}>`, **after** the Name `<Box>` and **before** the submit `<Button>` (order: Name → Description → Servings):

```tsx
                        <Box>
                            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                                {t("recipes.description")}
                            </Typography>
                            <TextField
                                fullWidth
                                multiline
                                minRows={2}
                                value={description}
                                onChange={(e) => setDescription(e.target.value)}
                                disabled={isLoading}
                                placeholder={t("recipes.descriptionPlaceholder")}
                                slotProps={{ htmlInput: { maxLength: 1000 } }}
                            />
                        </Box>

                        <Box>
                            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                                {t("recipes.servings")}
                            </Typography>
                            <TextField
                                type="number"
                                value={servings}
                                onChange={(e) => setServings(e.target.value)}
                                disabled={isLoading}
                                sx={{ width: 120 }}
                                slotProps={{ htmlInput: { min: 1, max: 99, "data-testid": "recipe-servings-input" } }}
                            />
                        </Box>
```

- [ ] **Step 3: Add the fields to `EditRecipeForm.tsx`**

Add `Typography` to the MUI import list (it currently imports `Box, Button, Card, CardContent, Stack, TextField`):

```tsx
import {
    Box,
    Button,
    Card,
    CardContent,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
```

Seed state alongside `editedName` (after that line):

```tsx
    const [editedDescription, setEditedDescription] = useState(recipe.description ?? "");
    const [editedServings, setEditedServings] = useState(
        recipe.servings != null ? String(recipe.servings) : "",
    );
```

Change the `handleSave` body to send all three:

```tsx
        updateRecipeMutation.mutate(
            {
                path: { householdId, recipeId: recipe.id },
                body: {
                    name: editedName.trim(),
                    description: editedDescription.trim() || null,
                    servings: editedServings === "" ? null : Number(editedServings),
                },
            },
            {
                onSuccess: () => router.history.back(),
            },
        );
```

Add the two fields inside `<Stack spacing={3}>`, **after** the Name `<TextField>` and **before** the buttons `<Box>` (order: Name → Description → Servings):

```tsx
                    <TextField
                        label={t("recipes.description")}
                        value={editedDescription}
                        onChange={(e) => setEditedDescription(e.target.value)}
                        fullWidth
                        multiline
                        minRows={2}
                        placeholder={t("recipes.descriptionPlaceholder")}
                        slotProps={{ htmlInput: { maxLength: 1000 } }}
                    />

                    <TextField
                        type="number"
                        label={t("recipes.servings")}
                        value={editedServings}
                        onChange={(e) => setEditedServings(e.target.value)}
                        sx={{ width: 140 }}
                        slotProps={{ htmlInput: { min: 1, max: 99, "data-testid": "recipe-servings-input" } }}
                    />
```

- [ ] **Step 4: Type-check and lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: no type errors, no lint errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/ Application/Frigorino.Web/ClientApp/public/locales/
git commit -m "feat: edit recipe description and servings in create/edit forms"
```

---

## Task 6: Quantity scaling util + chip accent prop

**Files:**
- Modify: `src/components/composer/features/quantityFormat.ts`
- Modify: `src/components/composer/index.ts` (barrel re-export)
- Modify: `src/components/common/ItemQuantityChip.tsx`

- [ ] **Step 1: Add `scaleQuantity` to `quantityFormat.ts`**

Append after `formatQuantity`:

```ts
// Multiply a structured quantity by a scale factor for DISPLAY only. Rounds to 3 decimals to match
// the numeric(12,3) DB column, so a scaled value shown here equals what a future promote-to-list
// would persist. Unit is unchanged (no g<->kg conversion). Caller skips this when multiplier === 1.
export const scaleQuantity = (q: QuantityDto, multiplier: number): QuantityDto => {
    const scaled = Number(q.value) * multiplier;
    const rounded = Math.round(scaled * 1000) / 1000;
    return { ...q, value: rounded };
};
```

(`QuantityDto` is already imported at the top of the file.)

- [ ] **Step 1b: Re-export `scaleQuantity` from the composer barrel**

In `src/components/composer/index.ts`, add `scaleQuantity` to the existing `quantityFormat` export block:

```ts
export {
    formatQuantity,
    scaleQuantity,
    unitLabel,
    QUANTITY_UNIT_VALUES,
} from "./features/quantityFormat";
```

- [ ] **Step 2: Add an optional `color` prop to `ItemQuantityChip.tsx`**

Add `color` to `Props` and forward it to the MUI `Chip` (default keeps today's look):

```tsx
import { Chip, type ChipProps } from "@mui/material";
```

```tsx
interface Props {
    quantity: QuantityDto;
    onClick?: () => void;
    testId?: string;
    color?: ChipProps["color"];
}

export function ItemQuantityChip({ quantity, onClick, testId, color }: Props) {
    const { t } = useTranslation();
    return (
        <Chip
            size="small"
            variant="outlined"
            color={color}
            data-testid={testId}
            label={formatQuantity(t, quantity)}
            onClick={onClick}
            sx={{ height: 20 }}
        />
    );
}
```

- [ ] **Step 3: Type-check and lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: clean (no consumers break — `color` is optional and additive).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/features/quantityFormat.ts Application/Frigorino.Web/ClientApp/src/components/common/ItemQuantityChip.tsx
git commit -m "feat: add scaleQuantity util and chip color prop"
```

---

## Task 7: Scaling control on the recipe view + threaded multiplier

**Files:**
- Modify: `src/features/recipes/pages/RecipeViewPage.tsx`
- Modify: `src/features/recipes/items/components/RecipeContainer.tsx`
- Modify: `src/features/recipes/items/components/RecipeItemContent.tsx`

- [ ] **Step 1: Apply scaling + struck-through original in `RecipeItemContent.tsx`**

Replace the whole file with:

```tsx
import { Box, ListItemText, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import { formatQuantity, scaleQuantity } from "../../../../components/composer";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
    // Display-only scale factor for quantities. 1 = unscaled (default).
    multiplier?: number;
}

export function RecipeItemContent({ item, multiplier = 1 }: Props) {
    const { t } = useTranslation();
    const isScaled = multiplier !== 1 && !!item.quantity;
    const displayQuantity =
        item.quantity && isScaled
            ? scaleQuantity(item.quantity, multiplier)
            : item.quantity;

    return (
        <ListItemText
            data-testid={`recipe-item-${item.id}`}
            slotProps={{ secondary: { component: "div" } }}
            primary={
                <Typography
                    variant="body2"
                    sx={{ fontWeight: 500, wordBreak: "break-word" }}
                >
                    {item.text}
                </Typography>
            }
            secondary={
                item.quantity || item.comment ? (
                    <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
                        {item.comment ? (
                            <Typography
                                component="div"
                                data-testid={`recipe-item-comment-${item.id}`}
                                variant="caption"
                                color="text.secondary"
                                sx={{
                                    fontSize: "0.7rem",
                                    fontStyle: "italic",
                                    whiteSpace: "pre-wrap",
                                    wordBreak: "break-word",
                                }}
                            >
                                {item.comment}
                            </Typography>
                        ) : null}
                        {displayQuantity ? (
                            <Box sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}>
                                <ItemQuantityChip
                                    quantity={displayQuantity}
                                    color={isScaled ? "primary" : undefined}
                                    testId={`recipe-item-quantity-${item.text}`}
                                />
                                {isScaled && item.quantity ? (
                                    <Typography
                                        component="span"
                                        data-testid={`recipe-item-quantity-base-${item.id}`}
                                        variant="caption"
                                        color="text.disabled"
                                        sx={{ textDecoration: "line-through", fontSize: "0.7rem" }}
                                    >
                                        {formatQuantity(t, item.quantity)}
                                    </Typography>
                                ) : null}
                            </Box>
                        ) : null}
                    </Box>
                ) : null
            }
        />
    );
}
```

> Both `formatQuantity` and `scaleQuantity` are exported from `src/components/composer/index.ts` (the barrel) — `formatQuantity` already was; `scaleQuantity` was added in Task 6 Step 1b.

- [ ] **Step 2: Thread `multiplier` through `RecipeContainer.tsx`**

Add `multiplier` to `RecipeContainerProps`:

```tsx
    searchQuery?: string;
    multiplier?: number;
```

Destructure it (with default) in the component params, alongside `searchQuery = ""`:

```tsx
            searchQuery = "",
            multiplier = 1,
```

Pass it in `renderContent`:

```tsx
                        renderContent={(item) => (
                            <RecipeItemContent item={item} multiplier={multiplier} />
                        )}
```

- [ ] **Step 3: Add the servings stepper + multiplier in `RecipeViewPage.tsx`**

Add imports at the top (extend the existing `@mui/material` import and icons import):

```tsx
import { Add, Edit, Remove, Search } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    IconButton,
    Stack,
    Typography,
} from "@mui/material";
```

Add scaling state + derived values after the existing `useState` hooks (e.g. after `pendingExtraction`):

```tsx
    // Display-only scaling. targetServings overrides the base; null = no override (shows base).
    const [targetServings, setTargetServings] = useState<number | null>(null);
```

After `recipe` is loaded (anywhere before the `return`, e.g. just before `const directActions`), derive:

```tsx
    const baseServings = recipe?.servings ?? null;
    const effectiveServings = targetServings ?? baseServings;
    const isScaled =
        baseServings != null &&
        effectiveServings != null &&
        effectiveServings !== baseServings;
    const multiplier =
        baseServings && effectiveServings ? effectiveServings / baseServings : 1;

    const stepServings = (delta: number) => {
        if (baseServings == null || effectiveServings == null) return;
        const next = Math.min(99, Math.max(1, effectiveServings + delta));
        setTargetServings(next);
    };
```

Render the stepper row between `<SearchInputRow ... />` and `<RecipeContainer ... />`, shown only when the recipe has a base servings count:

```tsx
            {baseServings != null ? (
                <Stack
                    direction="row"
                    alignItems="center"
                    justifyContent="space-between"
                    sx={{ px: 2, py: 0.5, borderBottom: 1, borderColor: "divider" }}
                >
                    <Typography variant="body2" color="text.secondary">
                        {t("recipes.servingsFrom", { count: baseServings })}
                    </Typography>
                    <Stack direction="row" alignItems="center" spacing={0.5}>
                        {isScaled ? (
                            <Button
                                size="small"
                                onClick={() => setTargetServings(null)}
                                data-testid="recipe-servings-reset"
                            >
                                {t("recipes.resetServings")}
                            </Button>
                        ) : null}
                        <IconButton
                            size="small"
                            onClick={() => stepServings(-1)}
                            disabled={effectiveServings != null && effectiveServings <= 1}
                            data-testid="recipe-servings-decrement"
                        >
                            <Remove fontSize="small" />
                        </IconButton>
                        <Typography
                            variant="body2"
                            sx={{ minWidth: 20, textAlign: "center", fontWeight: 600 }}
                            data-testid="recipe-servings-value"
                        >
                            {effectiveServings}
                        </Typography>
                        <IconButton
                            size="small"
                            onClick={() => stepServings(1)}
                            disabled={effectiveServings != null && effectiveServings >= 99}
                            data-testid="recipe-servings-increment"
                        >
                            <Add fontSize="small" />
                        </IconButton>
                    </Stack>
                </Stack>
            ) : null}
```

Pass the multiplier to `RecipeContainer` (add the prop to the existing element):

```tsx
            <RecipeContainer
                ref={scrollContainerRef}
                householdId={householdId}
                recipeId={recipeId}
                editingItem={editingItem}
                onEdit={setEditingItem}
                isExtracting={isExtracting}
                extractingItemId={extractingItemId}
                searchQuery={searchQuery}
                multiplier={multiplier}
            />
```

> `Add` is now used by both the stepper and (if present) any existing add affordance — it's imported once. Remove any now-unused icon imports flagged by lint.

- [ ] **Step 4: Type-check and lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: clean.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/
git commit -m "feat: display-only servings scaling on recipe view"
```

---

## Task 8: Build the SPA + manual verification + full verification gate

**Files:** none (verification only)

- [ ] **Step 1: Build the SPA** (the integration harness serves `ClientApp/build`, and manual verify needs current assets)

Run (from `ClientApp/`): `npm run build`
Expected: `tsc -b && vite build` succeeds, outputs to `ClientApp/build`.

- [ ] **Step 2: Bring up the dev stack and verify in the browser** (use the `/dev-up` skill)

Verify each behavior (dev-up data is disposable — mutate freely):
1. Create a recipe with a description and servings = 4 → both saved; description shows as the view header subtitle.
2. Add ingredients with quantities (e.g. "250 g flour", "2 eggs") and one without a quantity (e.g. "salt to taste").
3. On the view, the servings stepper shows "Servings (from 4)" with value 4 and **no Reset** link.
4. Increment to 8 → quantity chips turn accent-colored and show scaled values (500 g, 4 pc) with the struck-through originals (250 g, 2 pc); the no-quantity item is unchanged; **Reset** now appears.
5. Click **Reset** → back to base values, chips return to default styling, Reset disappears.
6. Open a recipe with **no servings** set → the stepper row is absent entirely; chips show stored values.
7. Edit the recipe → change description + servings; reopen → changes persisted.

- [ ] **Step 3: Full verification gate**

Run the full solution tests and the Docker build (per repo verification convention; do not parallelize — IT and a second test run share the Testcontainers port, and `npm run build` shares `ClientApp/build`):

```bash
dotnet test Application/Frigorino.sln
docker build -f Application/Dockerfile -t frigorino .
```
Expected: all tests pass; Docker build succeeds. (Ensure Docker Desktop is running; if the daemon is unreachable, ask the user to start it.)

- [ ] **Step 4: Tear down the dev stack** (use the `/dev-down` skill, only if you brought it up and the user doesn't want it kept)

---

## Self-Review (completed during planning)

- **Spec coverage:** Servings field + 1..99 validation (Task 1); slices/response/EF/migration (Task 2); IT round-trip + out-of-range (Task 3); client regen (Task 4); Description + Servings form inputs in Name→Description→Servings order (Task 5); `scaleQuantity` rounded to 3 dp + chip accent (Task 6); stepper with `(from N)` label, Reset-only-while-scaled, hidden when servings unset, struck-through original, multiplier threading (Task 7); manual + full verification (Task 8). All spec sections mapped.
- **Type consistency:** `Servings`/`servings` (int? / number|null) consistent across aggregate, DTOs, response, generated client, forms; `multiplier` prop (default 1) consistent across `RecipeViewPage` → `RecipeContainer` → `RecipeItemContent`; `scaleQuantity(q, multiplier)` signature consistent between definition (Task 6) and call (Task 7); `color` prop on `ItemQuantityChip` optional/additive.
- **No placeholders:** every code step shows full code; commands have expected output.
- **Migration note:** optional trailing `servings` params keep all existing `Create`/`Update` callers (incl. IT seeds) compiling.
