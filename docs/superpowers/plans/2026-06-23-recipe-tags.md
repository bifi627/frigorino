# Recipe Tags (Course + Dietary) with On-Demand AI Suggestion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give recipes a curated multi-select tag set (Course + Dietary facets), filterable on the overview, with an optional gated "Suggest tags" button that synchronously asks OpenAI for tag suggestions the user accepts as chips.

**Architecture:** A flat `RecipeTag` enum stored as a value-set on `Recipe` (one `integer[]` column, no join table). Writes go through a new `Recipe.SetTags` aggregate method (replace-whole-set, role-gated). Two new vertical slices: `PUT …/tags` (set) and `POST …/suggest-tags` (synchronous AI). The suggester sits behind a Domain port `IRecipeTagSuggester` with an OpenAI adapter + a Null impl, gated on a new `Ai:RecipeTagSuggester:*` config block, off by default. Overview tag filtering is client-side alongside the existing search. Suggestions are stateless — nothing persisted.

**Tech Stack:** .NET 10 minimal-API vertical slices, EF Core + Npgsql (Postgres), FluentResults, OpenAI SDK (Structured Outputs); React 19 + TanStack Router/Query + MUI; hey-api generated client; xUnit + FakeItEasy (unit) and Reqnroll + Playwright + Postgres Testcontainers (integration).

## Global Constraints

- **Spec:** `docs/superpowers/specs/2026-06-23-recipe-tags-design.md` — authoritative; this plan implements it.
- **Branch:** `feat/recipes-tags` (off `stage`, already created).
- **C# brace style:** always block `{}` braces, even single-line `if`/namespaces.
- **Enums on the wire:** serialize as string names (`JsonStringEnumConverter` already global). DB stores int.
- **Vertical slices only** — one file per endpoint, request+response DTO colocated, domain rules in the aggregate. No controllers. Reads = inline EF projection.
- **AI is vendor-agnostic:** the OpenAI SDK lives only in `Frigorino.Infrastructure/Services/`; the Domain port is `IRecipeTagSuggester`. Off by default (needs `Ai:ApiKey` **and** `Ai:RecipeTagSuggester:Enabled`).
- **No new DB tests in `Frigorino.Test`** — DB-touching behavior goes to `Frigorino.IntegrationTests` (Postgres Testcontainers). `Frigorino.Test` = pure aggregate/slice logic.
- **Tests never assert on translated text** — assert on testids / `data-*`.
- **i18n:** new keys go in `public/locales/{en,de}/translation.json` under the existing `recipes` namespace (JSON only — no `i18next.d.ts` change; that file types namespaces as `Record<string, string>`).
- **Frontend hooks:** spread generated `getXOptions` / `xMutation` / `getXQueryKey`; never hand-write `queryFn`/`mutationFn`/`queryKey`.
- **Client regen:** after any DTO/endpoint change, run `npm run api` from `ClientApp/` and commit `src/lib/api`.
- **Dependency pinning:** no new deps expected. If one sneaks in: NuGet exact, npm caret-minor.
- **Verification gates:** frontend `npm run lint` + `npm run tsc` + `npm run prettier`; backend `dotnet test Application/Frigorino.sln`; final `docker build -f Application/Dockerfile -t frigorino .`.

---

## File Structure

**Domain (`Application/Frigorino.Domain/`)**
- Create `Entities/RecipeTag.cs` — the enum (Course + Dietary).
- Modify `Entities/Recipe.cs` — `Tags` property, `MaxTags` const, `SetTags(...)` method.
- Create `Interfaces/IRecipeTagSuggester.cs` — the suggester port.

**Infrastructure (`Application/Frigorino.Infrastructure/`)**
- Modify `EntityFramework/Configurations/RecipeConfiguration.cs` — map `Tags` to `integer[]`.
- Create EF migration `…/Migrations/<timestamp>_AddRecipeTags.cs` (generated).
- Modify `Services/AiKeys.cs` — add the suggester key.
- Create `Services/OpenAiRecipeTagSuggester.cs` — OpenAI adapter (Structured Outputs).
- Create `Services/NullRecipeTagSuggester.cs` — no-op (returns empty).
- Create `Services/RecipeTagSuggestionDependencyInjection.cs` — `AddRecipeTagSuggestion`.

**Features (`Application/Frigorino.Features/Recipes/`)**
- Modify `RecipeResponse.cs` — add `Tags`.
- Create `Tags/SetRecipeTags.cs` — `PUT …/tags`.
- Create `Tags/SuggestRecipeTags.cs` — `POST …/suggest-tags`.

**Web (`Application/Frigorino.Web/`)**
- Modify `Program.cs` — register `AddRecipeTagSuggestion` + the two slices.
- Modify `appsettings.json` — add `Ai:RecipeTagSuggester` block.

**Frontend (`Application/Frigorino.Web/ClientApp/src/`)**
- Create `features/recipes/tags.ts` — facet arrays + label hook.
- Create `features/recipes/useSetRecipeTags.ts`, `features/recipes/useSuggestRecipeTags.ts`.
- Create `features/recipes/components/RecipeTagChips.tsx` — read-only display chips (view + summary).
- Create `features/recipes/components/RecipeTagSelector.tsx` — edit selector + suggest button + ghost chips.
- Create `features/recipes/components/RecipeTagFilter.tsx` — overview filter chip row.
- Modify `features/recipes/components/EditRecipeForm.tsx` — render the selector.
- Modify `features/recipes/pages/RecipeViewPage.tsx` — render read-only chips.
- Modify `features/recipes/pages/RecipesPage.tsx` — render filter + combine with search.
- Modify `public/locales/{en,de}/translation.json` — new keys.

**Tests**
- Modify `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs` — `SetTags` unit tests.
- Create `Application/Frigorino.IntegrationTests/Infrastructure/StubRecipeTagSuggester.cs`.
- Modify `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs` — register stub.
- Modify `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — tag seed helper.
- Create `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeTags.feature` + `RecipeTagSteps.cs`.

**Docs**
- Modify `knowledge/Recipes.md`, `knowledge/AI_Classification.md`, `CLAUDE.md`.

---

# Phase A — Backend domain + storage

### Task 1: `RecipeTag` enum

**Files:**
- Create: `Application/Frigorino.Domain/Entities/RecipeTag.cs`

**Interfaces:**
- Produces: `Frigorino.Domain.Entities.RecipeTag` enum with members listed below.

- [ ] **Step 1: Create the enum**

```csharp
namespace Frigorino.Domain.Entities
{
    // Curated recipe tags, two facets in one flat enum. Stored as int in an integer[] column on
    // Recipe (value-set; no join table). Serialized as string names on the wire
    // (JsonStringEnumConverter, global). The AI suggester's strict-output schema derives its allowed
    // values from Enum.GetNames<RecipeTag>(), so adding/removing a value updates that schema with no
    // hand-edit — but OpenAiRecipeTagSuggester's system prompt should describe new values.
    // No member at 0: a recipe with no fitting tag simply has an empty set. Numeric ranges group the
    // facets (Course 1–19, Dietary 20+); the frontend grouping uses explicit arrays, not the ranges.
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
}
```

- [ ] **Step 2: Build the Domain project**

Run: `dotnet build Application/Frigorino.Domain`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Domain/Entities/RecipeTag.cs
git commit -m "feat(recipes): add RecipeTag enum (course + dietary)"
```

---

### Task 2: `Recipe.Tags` + `Recipe.SetTags` aggregate method (TDD)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Recipe.cs`
- Test: `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs`

**Interfaces:**
- Consumes: `RecipeTag` (Task 1), existing `Recipe.CanBeManagedBy`, `AccessDeniedError`.
- Produces: `Recipe.Tags` (`List<RecipeTag>`), `Recipe.MaxTags` (`const int = 10`), `Result Recipe.SetTags(string callerUserId, HouseholdRole callerRole, IEnumerable<RecipeTag> tags)`.

- [ ] **Step 1: Write the failing tests**

Append to `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs` (inside the existing test class; mirror its `NewRecipe()` helper and assertion style):

```csharp
[Fact]
public void SetTags_ReplacesWholeSet_AndDeduplicates()
{
    var recipe = NewRecipe();

    var result = recipe.SetTags("u1", HouseholdRole.Member,
        new[] { RecipeTag.Main, RecipeTag.Vegetarian, RecipeTag.Main });

    Assert.True(result.IsSuccess);
    Assert.Equal(new[] { RecipeTag.Main, RecipeTag.Vegetarian }, recipe.Tags);
}

[Fact]
public void SetTags_EmptySet_ClearsTags()
{
    var recipe = NewRecipe();
    recipe.SetTags("u1", HouseholdRole.Member, new[] { RecipeTag.Main });

    var result = recipe.SetTags("u1", HouseholdRole.Member, Array.Empty<RecipeTag>());

    Assert.True(result.IsSuccess);
    Assert.Empty(recipe.Tags);
}

[Fact]
public void SetTags_OverCap_Fails()
{
    var recipe = NewRecipe();
    // 11 distinct values > MaxTags (10).
    var tooMany = new[]
    {
        RecipeTag.Breakfast, RecipeTag.Starter, RecipeTag.Main, RecipeTag.Side,
        RecipeTag.Salad, RecipeTag.Soup, RecipeTag.Dessert, RecipeTag.Snack,
        RecipeTag.Drink, RecipeTag.Sauce, RecipeTag.Baking,
    };

    var result = recipe.SetTags("u1", HouseholdRole.Member, tooMany);

    Assert.True(result.IsFailed);
    Assert.Equal("Tags", result.Errors[0].Metadata["Property"]);
}

[Fact]
public void SetTags_UnknownValue_Fails()
{
    var recipe = NewRecipe();

    var result = recipe.SetTags("u1", HouseholdRole.Member, new[] { (RecipeTag)999 });

    Assert.True(result.IsFailed);
    Assert.Equal("Tags", result.Errors[0].Metadata["Property"]);
}

[Fact]
public void SetTags_NonManager_Denied()
{
    var recipe = NewRecipe();

    var result = recipe.SetTags("someone-else", HouseholdRole.Member, new[] { RecipeTag.Main });

    Assert.True(result.IsFailed);
    Assert.IsType<AccessDeniedError>(result.Errors[0]);
}
```

Note: `NewRecipe()` creates the recipe with `createdByUserId: "u1"`, so `"u1"` is the manager and `"someone-else"` (Member) is not.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests.SetTags"`
Expected: FAIL — `Recipe` has no `Tags` / `SetTags`.

- [ ] **Step 3: Add the property + method**

In `Application/Frigorino.Domain/Entities/Recipe.cs`, add the constant near the other consts (line ~12):

```csharp
        public const int MaxTags = 10;
```

Add the property near the other scalar properties (after `IsActive`, line ~22):

```csharp
        public List<RecipeTag> Tags { get; set; } = [];
```

Add the method after `SoftDelete` (line ~96), before the section coordination region:

```csharp
        // Replace-whole-set semantics (matches a multi-select). Role-gated like Update/SoftDelete.
        // De-dupes, rejects unknown enum values and over-cap sets. An empty set clears all tags.
        public Result SetTags(string callerUserId, HouseholdRole callerRole, IEnumerable<RecipeTag> tags)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(new AccessDeniedError("Only the recipe creator or an admin can edit this recipe."));
            }

            var distinct = (tags ?? Enumerable.Empty<RecipeTag>()).Distinct().ToList();

            if (distinct.Any(t => !Enum.IsDefined(t)))
            {
                return Result.Fail(new Error("One or more tags are not recognized.")
                    .WithMetadata("Property", nameof(Tags)));
            }
            if (distinct.Count > MaxTags)
            {
                return Result.Fail(new Error($"A recipe can have at most {MaxTags} tags.")
                    .WithMetadata("Property", nameof(Tags)));
            }

            Tags = distinct;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests.SetTags"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Recipe.cs Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs
git commit -m "feat(recipes): Recipe.SetTags aggregate method + tests"
```

---

### Task 3: EF mapping + migration for `Tags` → `integer[]`

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddRecipeTags.cs` (generated)

**Interfaces:**
- Consumes: `Recipe.Tags` (Task 2).
- Produces: a non-null `integer[]` column `Tags` on `Recipes`, default `'{}'`.

- [ ] **Step 1: Configure the property**

In `RecipeConfiguration.cs`, add these usings at the top:

```csharp
using System.Collections.Generic;
using System.Linq;
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
```

Inside `Configure(...)`, after the `IsActive` property line, add:

```csharp
            // Value-set of curated tags stored as a native PostgreSQL integer[] column. A value
            // converter maps List<RecipeTag> <-> int[] (Npgsql maps int[] to integer[] natively), with
            // a value comparer so EF tracks element changes on the mutable list. Filtering is
            // client-side, so no index is needed here.
            builder.Property(r => r.Tags)
                .HasConversion(
                    v => v.Select(t => (int)t).ToArray(),
                    v => v.Select(i => (RecipeTag)i).ToList(),
                    new ValueComparer<List<RecipeTag>>(
                        (a, b) => a != null && b != null && a.SequenceEqual(b),
                        v => v.Aggregate(0, (hash, t) => System.HashCode.Combine(hash, (int)t)),
                        v => v.ToList()))
                .HasColumnType("integer[]")
                .HasDefaultValueSql("'{}'")
                .IsRequired();
```

- [ ] **Step 2: Generate the migration**

Run:
```bash
dotnet ef migrations add AddRecipeTags --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration file is created.

- [ ] **Step 3: Verify the generated migration**

Open the generated `…_AddRecipeTags.cs` and confirm `Up()` adds an `integer[]` column, NOT NULL, default `{}`. It should look like:

```csharp
migrationBuilder.AddColumn<int[]>(
    name: "Tags",
    table: "Recipes",
    type: "integer[]",
    nullable: false,
    defaultValueSql: "'{}'");
```

If the column type or nullability differs (e.g. it generated `jsonb` or nullable), the enum-array mapping resolved differently than expected — hand-edit `Up()`/`Down()` to the shape above and adjust the `RecipeConfiguration` accordingly so the round-trip test in Task 15 passes. (Fallback if Npgsql refuses `integer[]` for the converter: keep the same column but verify with the round-trip integration test; do not switch to a join table — the spec rules that out.)

- [ ] **Step 4: Build**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeConfiguration.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat(recipes): map Recipe.Tags to integer[] column + migration"
```

---

### Task 4: Expose `Tags` on `RecipeResponse`

**Files:**
- Modify: `Application/Frigorino.Features/Recipes/RecipeResponse.cs`

**Interfaces:**
- Consumes: `Recipe.Tags`.
- Produces: `RecipeResponse.Tags` (`IReadOnlyList<RecipeTag>`), populated by both `From` (from the entity) and `ToProjection` (from `r.Tags`).

- [ ] **Step 1: Add the field + populate both factories**

Edit `RecipeResponse.cs`. Add `using Frigorino.Domain.Entities;` (already present — `Recipe` is referenced). Add the record parameter at the end:

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
        int ItemCount,
        int? CoverAttachmentId,
        IReadOnlyList<string> Ingredients,
        IReadOnlyList<RecipeTag> Tags)
```

Update `From` (Tags **is** available on the entity, unlike Ingredients/CoverAttachmentId):

```csharp
        public static RecipeResponse From(Recipe recipe, User creator, int itemCount)
            => new(recipe.Id, recipe.Name, recipe.Description, recipe.Servings, recipe.HouseholdId,
                   recipe.CreatedAt, recipe.UpdatedAt,
                   new RecipeCreatorResponse(creator.ExternalId, creator.Name, creator.Email), itemCount,
                   CoverAttachmentId: null,
                   Ingredients: [],
                   Tags: recipe.Tags.ToList());
```

Update `ToProjection` — add `r.Tags.ToList()` as the final argument:

```csharp
            r.Items
                .Where(i => i.IsActive)
                .OrderBy(i => i.Section.Rank)
                .ThenBy(i => i.Rank)
                .Select(i => i.Text)
                .ToList(),
            r.Tags);
```

Note: project `r.Tags` directly (no in-Expression `.ToList()`) — EF materializes the value-converted property; `List<RecipeTag>` satisfies the `IReadOnlyList<RecipeTag>` parameter. The Task 15 round-trip/filter scenarios gate that this projection translates.

- [ ] **Step 2: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Recipes/RecipeResponse.cs
git commit -m "feat(recipes): expose Tags on RecipeResponse"
```

---

# Phase B — Backend slices + AI suggester

### Task 5: `SetRecipeTags` slice (`PUT …/tags`)

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Tags/SetRecipeTags.cs`

**Interfaces:**
- Consumes: `Recipe.SetTags`, `RecipeResponse.From`, `db.FindActiveMembershipAsync`, `result.ToValidationProblem()`.
- Produces: `IEndpointRouteBuilder MapSetRecipeTags(this IEndpointRouteBuilder app)`; route `PUT /{recipeId:int}/tags`; request `SetRecipeTagsRequest(IReadOnlyList<RecipeTag> Tags)`.

- [ ] **Step 1: Create the slice**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Tags
{
    public sealed record SetRecipeTagsRequest(IReadOnlyList<RecipeTag> Tags);

    public static class SetRecipeTagsEndpoint
    {
        public static IEndpointRouteBuilder MapSetRecipeTags(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{recipeId:int}/tags", Handle)
               .WithName("SetRecipeTags")
               .Produces<RecipeResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status403Forbidden)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId, int recipeId, SetRecipeTagsRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var recipe = await db.Recipes
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var result = recipe.SetTags(currentUser.UserId, membership.Role, request.Tags ?? []);
            if (result.IsFailed)
            {
                if (result.Errors[0] is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            var itemCount = await db.RecipeItems.CountAsync(i => i.RecipeId == recipeId && i.IsActive, ct);
            return TypedResults.Ok(RecipeResponse.From(recipe, recipe.CreatedByUser, itemCount));
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: build succeeds (the endpoint isn't wired yet — Task 9 wires it; this just compiles).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Tags/SetRecipeTags.cs
git commit -m "feat(recipes): SetRecipeTags slice (PUT tags)"
```

---

### Task 6: `IRecipeTagSuggester` port + Null impl + DI + config

**Files:**
- Create: `Application/Frigorino.Domain/Interfaces/IRecipeTagSuggester.cs`
- Create: `Application/Frigorino.Infrastructure/Services/NullRecipeTagSuggester.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/AiKeys.cs`
- Create: `Application/Frigorino.Infrastructure/Services/RecipeTagSuggestionDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/appsettings.json`

**Interfaces:**
- Produces: `IRecipeTagSuggester.SuggestAsync(string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct) -> Task<IReadOnlyList<RecipeTag>>`; `AiKeys.RecipeTagSuggester`; `IServiceCollection AddRecipeTagSuggestion(this IServiceCollection, IConfiguration)`.

Note: the port returns the tags directly (not `Result<T>`) — an empty list is a valid "no confident tags" answer, and any adapter error is swallowed to an empty list so a user's button tap never 500s.

- [ ] **Step 1: Create the port**

`Application/Frigorino.Domain/Interfaces/IRecipeTagSuggester.cs`:

```csharp
using Frigorino.Domain.Entities;

namespace Frigorino.Domain.Interfaces
{
    // The ONLY recipe-tag AI abstraction. The OpenAI SDK never crosses this boundary into
    // Domain/Features. Called synchronously and on-demand by the SuggestRecipeTags slice (the
    // deliberate, narrow exception to "AI runs fire-and-forget"). Returns the suggested tags
    // directly: an empty list means "no confident suggestions" (also the disabled/no-op result),
    // and adapter errors are swallowed to empty so the user's tap never fails.
    public interface IRecipeTagSuggester
    {
        Task<IReadOnlyList<RecipeTag>> SuggestAsync(
            string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct);
    }
}
```

- [ ] **Step 2: Create the Null impl**

`Application/Frigorino.Infrastructure/Services/NullRecipeTagSuggester.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;

namespace Frigorino.Infrastructure.Services
{
    // Registered when the feature is disabled (no API key / flag off). Always returns no suggestions,
    // so the SuggestRecipeTags endpoint is always safe to call.
    public sealed class NullRecipeTagSuggester : IRecipeTagSuggester
    {
        private static readonly IReadOnlyList<RecipeTag> Empty = [];

        public Task<IReadOnlyList<RecipeTag>> SuggestAsync(
            string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct)
            => Task.FromResult(Empty);
    }
}
```

- [ ] **Step 3: Add the keyed-client key**

In `AiKeys.cs`, add:

```csharp
        public const string RecipeTagSuggester = "ai-recipe-tag-suggester";
```

- [ ] **Step 4: Create the DI extension**

`Application/Frigorino.Infrastructure/Services/RecipeTagSuggestionDependencyInjection.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeTagSuggestionDependencyInjection
    {
        // Standalone — depends on no other AI port. Gated on Ai:ApiKey + Ai:RecipeTagSuggester:Enabled.
        // Registers IRecipeTagSuggester on BOTH paths (real or Null), so the suggest slice can always
        // resolve it. Synchronous on-demand suggester — no job/trigger/queue.
        public static IServiceCollection AddRecipeTagSuggestion(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:RecipeTagSuggester:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            var model = configuration["Ai:RecipeTagSuggester:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-5.4-mini";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddKeyedSingleton<ChatClient>(
                    AiKeys.RecipeTagSuggester, new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IRecipeTagSuggester, OpenAiRecipeTagSuggester>();
            }
            else
            {
                services.AddScoped<IRecipeTagSuggester, NullRecipeTagSuggester>();
            }

            return services;
        }
    }
}
```

Note: `OpenAiRecipeTagSuggester` doesn't exist yet (Task 7) — this file won't compile until Task 7 lands. That's fine; commit them together at the end of Task 7. To keep this task independently buildable, do **Step 5 of Task 7** before building.

- [ ] **Step 5: Add the config block**

In `Application/Frigorino.Web/appsettings.json`, extend the `Ai` section:

```json
"Ai": {
    "ApiKey": "",
    "Classifier": { "Enabled": true, "Model": "gpt-5.4-mini" },
    "QuantityExtractor": { "Enabled": true, "Model": "gpt-5.4-nano" },
    "RecipeTagSuggester": { "Enabled": true, "Model": "gpt-5.4-mini" }
}
```

(`Enabled: true` here is harmless — the feature still no-ops until `Ai:ApiKey` is supplied via secrets/env.)

- [ ] **Step 6: Commit (with Task 7)** — see Task 7 Step 6.

---

### Task 7: `OpenAiRecipeTagSuggester` adapter (Structured Outputs)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/OpenAiRecipeTagSuggester.cs`

**Interfaces:**
- Consumes: `AiKeys.RecipeTagSuggester`, `IRecipeTagSuggester`, `RecipeTag`.
- Produces: `OpenAiRecipeTagSuggester : IRecipeTagSuggester`.

- [ ] **Step 1: Create the adapter**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Suggests curated recipe tags from a recipe's name + description + ingredients,
    // using strict Structured Outputs whose allowed values are interpolated from RecipeTag so they
    // can't drift. Refusals / empties / errors map to an empty list (a valid "no suggestions"). The
    // model's reasoning is logged for diagnostics only, never persisted nor returned.
    public sealed class OpenAiRecipeTagSuggester : IRecipeTagSuggester
    {
        // "reasoning" is FIRST (strict outputs generate fields in schema order → cheap chain-of-thought).
        private static readonly BinaryData Schema = BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "reasoning": { "type": "string" },
                    "tags": {
                        "type": "array",
                        "items": {
                            "type": "string",
                            "enum": [{{string.Join(", ", Enum.GetNames<RecipeTag>().Select(n => $"\"{n}\""))}}]
                        }
                    }
                },
                "required": ["reasoning", "tags"],
                "additionalProperties": false
            }
            """);

        private static readonly string SystemPrompt =
            "You assign curated category tags to a household recipe. You are given the recipe name, an optional description, and the ingredient lines (English or German).\n" +
            "Choose only tags that clearly apply. It is fine to return an empty list when nothing is confident.\n" +
            "Course tags (pick at most one or two that fit): Breakfast, Starter, Main, Side, Salad, Soup, Dessert, Snack, Drink, Sauce, Baking, Bread.\n" +
            "Dietary tags (only when the ingredients clearly support it): Vegetarian (no meat/fish), Vegan (no animal products at all), GlutenFree, DairyFree (no dairy at all), LactoseFree (low/no lactose but may contain dairy proteins), LowCarb.\n" +
            "Do not guess dietary tags from the name alone — require ingredient evidence. Do not invent tags outside the provided enum.\n" +
            "In 'reasoning', briefly justify your choices in one short English sentence regardless of input language.\n" +
            "Respond only via the provided JSON schema.";

        private static readonly SystemChatMessage SystemMessage = new(SystemPrompt);

        private static readonly ChatCompletionOptions Options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "recipe_tag_suggestion",
                jsonSchema: Schema,
                jsonSchemaIsStrict: true),
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private sealed record SuggesterResponse(string Reasoning, RecipeTag[] Tags);

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiRecipeTagSuggester> _logger;

        public OpenAiRecipeTagSuggester(
            [FromKeyedServices(AiKeys.RecipeTagSuggester)] ChatClient client,
            ILogger<OpenAiRecipeTagSuggester> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IReadOnlyList<RecipeTag>> SuggestAsync(
            string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct)
        {
            var prompt =
                $"Name: {name}\n" +
                $"Description: {(string.IsNullOrWhiteSpace(description) ? "(none)" : description)}\n" +
                $"Ingredients:\n{(ingredients.Count == 0 ? "(none)" : string.Join("\n", ingredients.Select(i => "- " + i)))}";

            var messages = new ChatMessage[] { SystemMessage, new UserChatMessage(prompt) };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, Options, ct);

                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning("Tag suggester returned no usable content for '{Name}'.", name);
                    return [];
                }

                var dto = JsonSerializer.Deserialize<SuggesterResponse>(completion.Value.Content[0].Text, JsonOptions);
                if (dto is null)
                {
                    return [];
                }

                // Defensive: distinct + drop anything not a defined enum value (strict schema should
                // prevent this, but never trust the model).
                var tags = dto.Tags
                    .Where(t => Enum.IsDefined(t))
                    .Distinct()
                    .ToList();

                _logger.LogInformation(
                    "Suggested tags for '{Name}': {Tags} ({Reasoning})",
                    name, string.Join(",", tags), dto.Reasoning);

                return tags;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Tag suggester call failed for '{Name}'.", name);
                return [];
            }
        }
    }
}
```

- [ ] **Step 2: Build Infrastructure**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: build succeeds (Task 6's DI file now compiles too).

- [ ] **Step 3: Commit (Tasks 6 + 7 together)**

```bash
git add Application/Frigorino.Domain/Interfaces/IRecipeTagSuggester.cs \
        Application/Frigorino.Infrastructure/Services/NullRecipeTagSuggester.cs \
        Application/Frigorino.Infrastructure/Services/OpenAiRecipeTagSuggester.cs \
        Application/Frigorino.Infrastructure/Services/RecipeTagSuggestionDependencyInjection.cs \
        Application/Frigorino.Infrastructure/Services/AiKeys.cs \
        Application/Frigorino.Web/appsettings.json
git commit -m "feat(recipes): IRecipeTagSuggester port + OpenAI adapter + DI/config"
```

---

### Task 8: `SuggestRecipeTags` slice (`POST …/suggest-tags`)

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Tags/SuggestRecipeTags.cs`

**Interfaces:**
- Consumes: `IRecipeTagSuggester`, `db.FindActiveMembershipAsync`, `RecipeTag`.
- Produces: `IEndpointRouteBuilder MapSuggestRecipeTags(this IEndpointRouteBuilder app)`; route `POST /{recipeId:int}/suggest-tags`; response `SuggestRecipeTagsResponse(IReadOnlyList<RecipeTag> SuggestedTags)`.

- [ ] **Step 1: Create the slice**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Tags
{
    public sealed record SuggestRecipeTagsResponse(IReadOnlyList<RecipeTag> SuggestedTags);

    public static class SuggestRecipeTagsEndpoint
    {
        public static IEndpointRouteBuilder MapSuggestRecipeTags(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{recipeId:int}/suggest-tags", Handle)
               .WithName("SuggestRecipeTags")
               .Produces<SuggestRecipeTagsResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        // Synchronous, on-demand AI — the deliberate, narrow exception to "AI never inline" (this is
        // the user's primary action, not a side-effect of a write). Stateless: nothing is persisted.
        private static async Task<Results<Ok<SuggestRecipeTagsResponse>, NotFound>> Handle(
            int householdId, int recipeId,
            ICurrentUserService currentUser, IRecipeTagSuggester suggester,
            ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var recipe = await db.Recipes
                .Where(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive)
                .Select(r => new { r.Name, r.Description })
                .FirstOrDefaultAsync(ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var ingredients = await db.RecipeItems
                .Where(i => i.RecipeId == recipeId && i.IsActive)
                .OrderBy(i => i.Section.Rank)
                .ThenBy(i => i.Rank)
                .Select(i => i.Text)
                .ToListAsync(ct);

            var suggested = await suggester.SuggestAsync(recipe.Name, recipe.Description, ingredients, ct);
            return TypedResults.Ok(new SuggestRecipeTagsResponse(suggested));
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Tags/SuggestRecipeTags.cs
git commit -m "feat(recipes): SuggestRecipeTags slice (synchronous on-demand AI)"
```

---

### Task 9: Wire `Program.cs` + regenerate the client

**Files:**
- Modify: `Application/Frigorino.Web/Program.cs`
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/api/**` + `openapi.json`

**Interfaces:**
- Consumes: `MapSetRecipeTags`, `MapSuggestRecipeTags`, `AddRecipeTagSuggestion`.
- Produces: registered endpoints; regenerated TS client with `setRecipeTags`/`suggestRecipeTags` operations + `RecipeTag` type.

- [ ] **Step 1: Register the DI extension**

In `Program.cs`, after the line `builder.Services.AddRecipeQuantityExtraction(builder.Configuration);` (~line 96), add:

```csharp
builder.Services.AddRecipeTagSuggestion(builder.Configuration);
```

Add the using if not present (it's the same namespace as the others — `Frigorino.Infrastructure.Services`, already imported).

- [ ] **Step 2: Register the slices**

In the recipes group block (~line 425), after `recipes.MapCopyRecipeToList();`, add:

```csharp
recipes.MapSetRecipeTags();
recipes.MapSuggestRecipeTags();
```

Add `using Frigorino.Features.Recipes.Tags;` to the usings at the top of `Program.cs` if the slice extension methods aren't resolved (they're in the `Frigorino.Features.Recipes.Tags` namespace).

- [ ] **Step 3: Regenerate the API client**

Run from `Application/Frigorino.Web/ClientApp/`:
```bash
npm run api
```
Expected: `src/lib/openapi.json` regenerates and `src/lib/api` gains `setRecipeTags*` / `suggestRecipeTags*` exports and a `RecipeTag` string-union type in `types.gen.ts`.

- [ ] **Step 4: Verify the generated type**

Run: `grep -r "RecipeTag" Application/Frigorino.Web/ClientApp/src/lib/api/types.gen.ts`
Expected: `export type RecipeTag = 'Breakfast' | 'Starter' | ... | 'LowCarb';`

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/Program.cs Application/Frigorino.Web/ClientApp/src/lib
git commit -m "feat(recipes): wire tag slices + regenerate API client"
```

---

# Phase C — Frontend

### Task 10: Tag vocabulary helper + i18n keys

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/tags.ts`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

**Interfaces:**
- Consumes: generated `RecipeTag` type.
- Produces: `COURSE_TAGS`, `DIETARY_TAGS`, `ALL_TAGS` (`RecipeTag[]`); `useTagLabel()` → `(tag: RecipeTag) => string`.

- [ ] **Step 1: Create `tags.ts`**

```typescript
import { useTranslation } from "react-i18next";
import { useCallback } from "react";
import type { RecipeTag } from "../../lib/api";

// Facet ordering for every surface (selector, view, filter). The numeric enum ranges group the
// facets on the backend; the frontend uses these explicit arrays.
export const COURSE_TAGS: readonly RecipeTag[] = [
    "Breakfast",
    "Starter",
    "Main",
    "Side",
    "Salad",
    "Soup",
    "Dessert",
    "Snack",
    "Drink",
    "Sauce",
    "Baking",
    "Bread",
];

export const DIETARY_TAGS: readonly RecipeTag[] = [
    "Vegetarian",
    "Vegan",
    "GlutenFree",
    "DairyFree",
    "LactoseFree",
    "LowCarb",
];

export const ALL_TAGS: readonly RecipeTag[] = [...COURSE_TAGS, ...DIETARY_TAGS];

// Translated label for a tag, e.g. "recipes.tagLabels.GlutenFree" -> "Gluten-free".
export const useTagLabel = () => {
    const { t } = useTranslation();
    return useCallback((tag: RecipeTag) => t(`recipes.tagLabels.${tag}`), [t]);
};
```

- [ ] **Step 2: Add the English keys**

In `en/translation.json`, inside the `recipes` object, add:

```json
"tagsHeading": "Tags",
"courseHeading": "Course",
"dietaryHeading": "Dietary",
"suggestTags": "Suggest tags",
"noTagSuggestions": "No tag suggestions",
"filterByTags": "Filter by tags",
"tagLabels": {
    "Breakfast": "Breakfast",
    "Starter": "Starter",
    "Main": "Main",
    "Side": "Side",
    "Salad": "Salad",
    "Soup": "Soup",
    "Dessert": "Dessert",
    "Snack": "Snack",
    "Drink": "Drink",
    "Sauce": "Sauce",
    "Baking": "Baking",
    "Bread": "Bread",
    "Vegetarian": "Vegetarian",
    "Vegan": "Vegan",
    "GlutenFree": "Gluten-free",
    "DairyFree": "Dairy-free",
    "LactoseFree": "Lactose-free",
    "LowCarb": "Low-carb"
}
```

- [ ] **Step 3: Add the German keys**

In `de/translation.json`, inside the `recipes` object, add:

```json
"tagsHeading": "Tags",
"courseHeading": "Gang",
"dietaryHeading": "Ernährung",
"suggestTags": "Tags vorschlagen",
"noTagSuggestions": "Keine Tag-Vorschläge",
"filterByTags": "Nach Tags filtern",
"tagLabels": {
    "Breakfast": "Frühstück",
    "Starter": "Vorspeise",
    "Main": "Hauptgericht",
    "Side": "Beilage",
    "Salad": "Salat",
    "Soup": "Suppe",
    "Dessert": "Dessert",
    "Snack": "Snack",
    "Drink": "Getränk",
    "Sauce": "Soße",
    "Baking": "Backen",
    "Bread": "Brot",
    "Vegetarian": "Vegetarisch",
    "Vegan": "Vegan",
    "GlutenFree": "Glutenfrei",
    "DairyFree": "Milchfrei",
    "LactoseFree": "Laktosefrei",
    "LowCarb": "Low Carb"
}
```

- [ ] **Step 4: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: passes (no `.d.ts` change needed — `recipes` is typed `Record<string, string>` loosely; nested `tagLabels.*` keys resolve).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/tags.ts Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat(recipes): tag vocabulary helper + i18n labels"
```

---

### Task 11: Tag hooks (`useSetRecipeTags`, `useSuggestRecipeTags`)

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/useSetRecipeTags.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/useSuggestRecipeTags.ts`

**Interfaces:**
- Consumes: generated `setRecipeTagsMutation`, `suggestRecipeTagsMutation`, `getRecipeQueryKey`, `getRecipesQueryKey`.
- Produces: `useSetRecipeTags()`, `useSuggestRecipeTags()` mutation hooks.

- [ ] **Step 1: Create `useSetRecipeTags.ts`** (mirror `useUpdateRecipe.ts`)

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeQueryKey,
    getRecipesQueryKey,
    setRecipeTagsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useSetRecipeTags = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...setRecipeTagsMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

- [ ] **Step 2: Create `useSuggestRecipeTags.ts`** (no cache writes — caller reads the result)

```typescript
import { useMutation } from "@tanstack/react-query";
import { suggestRecipeTagsMutation } from "../../lib/api/@tanstack/react-query.gen";

// Stateless on-demand suggestion: the caller passes { path: { householdId, recipeId } } to
// mutateAsync and reads response.suggestedTags. No cache invalidation — suggestions aren't persisted.
export const useSuggestRecipeTags = () =>
    useMutation({
        ...suggestRecipeTagsMutation(),
    });
```

- [ ] **Step 3: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: passes (the generated names exist after Task 9).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/useSetRecipeTags.ts Application/Frigorino.Web/ClientApp/src/features/recipes/useSuggestRecipeTags.ts
git commit -m "feat(recipes): tag set + suggest hooks"
```

---

### Task 12: Read-only tag chips component

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeTagChips.tsx`

**Interfaces:**
- Consumes: `RecipeTag`, `useTagLabel`, `ALL_TAGS`.
- Produces: `<RecipeTagChips tags={...} />` — renders ordered, labelled, read-only chips; renders nothing when empty.

- [ ] **Step 1: Create the component**

```tsx
import { Box, Chip } from "@mui/material";
import type { RecipeTag } from "../../../lib/api";
import { ALL_TAGS, useTagLabel } from "../tags";

interface RecipeTagChipsProps {
    tags: RecipeTag[];
    size?: "small" | "medium";
}

// Read-only tag display (recipe view + summary peek). Orders by the canonical facet order and
// renders a testid per tag. Renders nothing when there are no tags.
export const RecipeTagChips = ({ tags, size = "small" }: RecipeTagChipsProps) => {
    const tagLabel = useTagLabel();
    if (!tags || tags.length === 0) {
        return null;
    }
    const ordered = ALL_TAGS.filter((t) => tags.includes(t));
    return (
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
            {ordered.map((tag) => (
                <Chip
                    key={tag}
                    label={tagLabel(tag)}
                    size={size}
                    variant="outlined"
                    data-testid={`recipe-tag-${tag}`}
                />
            ))}
        </Box>
    );
};
```

- [ ] **Step 2: Type-check + lint**

Run from `ClientApp/`: `npm run tsc && npm run lint`
Expected: passes.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeTagChips.tsx
git commit -m "feat(recipes): read-only RecipeTagChips component"
```

---

### Task 13: Tag selector (edit page) + suggest button

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeTagSelector.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx`

**Interfaces:**
- Consumes: `RecipeResponse` (`recipe.tags`), `useSetRecipeTags`, `useSuggestRecipeTags`, `COURSE_TAGS`, `DIETARY_TAGS`, `useTagLabel`.
- Produces: `<RecipeTagSelector householdId={} recipe={} />`.

- [ ] **Step 1: Create the selector**

Behavior: grouped selectable chips (filled when selected) per facet; toggling fires `useSetRecipeTags` with the full new set and optimistically updates local state. A "Suggest tags" button calls `useSuggestRecipeTags`; returned tags not already selected render as outlined **ghost** chips below; tapping a ghost chip selects it (normal set write) and removes it from the ghost row. Ghost chips are ephemeral local state.

```tsx
import { AutoAwesome } from "@mui/icons-material";
import {
    Box,
    Button,
    Chip,
    CircularProgress,
    Stack,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse, RecipeTag } from "../../../lib/api";
import { COURSE_TAGS, DIETARY_TAGS, useTagLabel } from "../tags";
import { useSetRecipeTags } from "../useSetRecipeTags";
import { useSuggestRecipeTags } from "../useSuggestRecipeTags";

interface RecipeTagSelectorProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const RecipeTagSelector = ({
    householdId,
    recipe,
}: RecipeTagSelectorProps) => {
    const { t } = useTranslation();
    const tagLabel = useTagLabel();
    const setTags = useSetRecipeTags();
    const suggest = useSuggestRecipeTags();

    // Optimistic local copy of the selected set so chip toggles feel instant. Seeded once on mount;
    // the parent renders EditRecipeForm with key={recipe.id}, so switching recipes remounts this and
    // re-seeds — no reset effect needed.
    const [selected, setSelected] = useState<RecipeTag[]>(recipe.tags ?? []);
    const [ghosts, setGhosts] = useState<RecipeTag[]>([]);

    const persist = (next: RecipeTag[]) => {
        setSelected(next);
        setTags.mutate({
            path: { householdId, recipeId: recipe.id },
            body: { tags: next },
        });
    };

    const toggle = (tag: RecipeTag) => {
        const next = selected.includes(tag)
            ? selected.filter((x) => x !== tag)
            : [...selected, tag];
        persist(next);
    };

    const acceptGhost = (tag: RecipeTag) => {
        setGhosts((g) => g.filter((x) => x !== tag));
        if (!selected.includes(tag)) {
            persist([...selected, tag]);
        }
    };

    const handleSuggest = async () => {
        const res = await suggest.mutateAsync({
            path: { householdId, recipeId: recipe.id },
        });
        setGhosts((res.suggestedTags ?? []).filter((tg) => !selected.includes(tg)));
    };

    const renderGroup = (heading: string, tags: readonly RecipeTag[]) => (
        <Box>
            <Typography variant="overline" sx={{ color: "text.secondary" }}>
                {heading}
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, mt: 0.5 }}>
                {tags.map((tag) => {
                    const isSelected = selected.includes(tag);
                    return (
                        <Chip
                            key={tag}
                            label={tagLabel(tag)}
                            size="small"
                            color={isSelected ? "primary" : "default"}
                            variant={isSelected ? "filled" : "outlined"}
                            onClick={() => toggle(tag)}
                            data-testid={`recipe-tag-select-${tag}`}
                        />
                    );
                })}
            </Box>
        </Box>
    );

    return (
        <Stack spacing={1} data-testid="recipe-tag-selector">
            <Typography variant="overline" sx={{ color: "text.secondary" }}>
                {t("recipes.tagsHeading")}
            </Typography>
            {renderGroup(t("recipes.courseHeading"), COURSE_TAGS)}
            {renderGroup(t("recipes.dietaryHeading"), DIETARY_TAGS)}

            <Box>
                <Button
                    size="small"
                    variant="text"
                    startIcon={
                        suggest.isPending ? (
                            <CircularProgress size={16} />
                        ) : (
                            <AutoAwesome fontSize="small" />
                        )
                    }
                    onClick={handleSuggest}
                    disabled={suggest.isPending}
                    data-testid="recipe-suggest-tags"
                >
                    {t("recipes.suggestTags")}
                </Button>
                {ghosts.length > 0 && (
                    <Box
                        sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, mt: 0.5 }}
                    >
                        {ghosts.map((tag) => (
                            <Chip
                                key={tag}
                                label={tagLabel(tag)}
                                size="small"
                                variant="outlined"
                                color="secondary"
                                onClick={() => acceptGhost(tag)}
                                data-testid={`recipe-tag-suggested-${tag}`}
                            />
                        ))}
                    </Box>
                )}
            </Box>
        </Stack>
    );
};
```

- [ ] **Step 2: Render it in `EditRecipeForm.tsx`**

Add the import:

```tsx
import { RecipeTagSelector } from "./RecipeTagSelector";
```

In the returned JSX, insert the selector after the description `<TextField>` and before the `data-testid="recipe-metadata-status"` box:

```tsx
            <RecipeTagSelector householdId={householdId} recipe={recipe} />
```

(`EditRecipeForm` already receives `householdId` and `recipe` props.)

- [ ] **Step 3: Type-check + lint**

Run from `ClientApp/`: `npm run tsc && npm run lint`
Expected: passes (the scoped `eslint-disable` covers the reset-on-change effect).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeTagSelector.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx
git commit -m "feat(recipes): edit-page tag selector + suggest button"
```

---

### Task 14: View-page tags + overview filter

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeTagFilter.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeViewPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx`

**Interfaces:**
- Consumes: `RecipeTagChips`, `RecipeResponse`, `COURSE_TAGS`, `DIETARY_TAGS`, `useTagLabel`, `rankRecipes`.
- Produces: `<RecipeTagFilter selected={} onToggle={} />`; view-page tag chips; overview tag-filter + AND combine with search.

- [ ] **Step 1: View page — render read-only chips**

In `RecipeViewPage.tsx`, add the import:

```tsx
import { RecipeTagChips } from "../components/RecipeTagChips";
```

After the description `<Container>…</Container>` block (after line ~177) and before the links section, add:

```tsx
{recipe.tags && recipe.tags.length > 0 ? (
    <Container maxWidth="sm" sx={{ px: 2, pb: 1.5 }}>
        <RecipeTagChips tags={recipe.tags} />
    </Container>
) : null}
```

- [ ] **Step 2: Create the overview filter component**

```tsx
import { Box, Chip, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeTag } from "../../../lib/api";
import { COURSE_TAGS, DIETARY_TAGS, useTagLabel } from "../tags";

interface RecipeTagFilterProps {
    selected: RecipeTag[];
    onToggle: (tag: RecipeTag) => void;
}

// Overview filter chip row, grouped by facet. Selecting chips narrows the list (AND across
// selected tags, combined with the search query in RecipesPage).
export const RecipeTagFilter = ({ selected, onToggle }: RecipeTagFilterProps) => {
    const { t } = useTranslation();
    const tagLabel = useTagLabel();

    const renderRow = (label: string, tags: readonly RecipeTag[]) => (
        <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {label}
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, mt: 0.25 }}>
                {tags.map((tag) => {
                    const isSelected = selected.includes(tag);
                    return (
                        <Chip
                            key={tag}
                            label={tagLabel(tag)}
                            size="small"
                            color={isSelected ? "primary" : "default"}
                            variant={isSelected ? "filled" : "outlined"}
                            onClick={() => onToggle(tag)}
                            data-testid={`recipe-filter-tag-${tag}`}
                        />
                    );
                })}
            </Box>
        </Box>
    );

    return (
        <Stack spacing={1} sx={{ mb: 2 }} data-testid="recipe-tag-filter">
            {renderRow(t("recipes.courseHeading"), COURSE_TAGS)}
            {renderRow(t("recipes.dietaryHeading"), DIETARY_TAGS)}
        </Stack>
    );
};
```

- [ ] **Step 3: Wire the filter into `RecipesPage.tsx`**

Add imports:

```tsx
import type { RecipeTag } from "../../../lib/api";
import { RecipeTagFilter } from "../components/RecipeTagFilter";
```

Add state next to `query`:

```tsx
    const [selectedTags, setSelectedTags] = useState<RecipeTag[]>([]);

    const toggleTag = (tag: RecipeTag) =>
        setSelectedTags((cur) =>
            cur.includes(tag) ? cur.filter((x) => x !== tag) : [...cur, tag],
        );
```

Replace the `visibleRecipes` memo so the tag filter (AND across selected tags) runs before search ranking:

```tsx
    const visibleRecipes = useMemo(() => {
        const all = recipes ?? [];
        const byTags =
            selectedTags.length === 0
                ? all
                : all.filter((r) =>
                      selectedTags.every((tag) => (r.tags ?? []).includes(tag)),
                  );
        return rankRecipes(byTags, query);
    }, [recipes, query, selectedTags]);
```

Render `<RecipeTagFilter>` inside the `recipes && recipes.length > 0` block, immediately before the search `<TextField>`:

```tsx
                        <RecipeTagFilter
                            selected={selectedTags}
                            onToggle={toggleTag}
                        />
```

- [ ] **Step 4: Type-check + lint + prettier**

Run from `ClientApp/`: `npm run tsc && npm run lint && npm run prettier`
Expected: passes.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeTagFilter.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeViewPage.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx
git commit -m "feat(recipes): view-page tags + overview tag filter"
```

---

# Phase D — Integration tests, docs, verification

### Task 15: Integration tests (round-trip, filter, suggest)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Infrastructure/StubRecipeTagSuggester.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeTags.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeTagSteps.cs`

**Interfaces:**
- Consumes: `IRecipeTagSuggester`, `RecipeTag`, existing IT infra (`ScenarioContextHolder`, `TestApiClient`, Playwright page).
- Produces: deterministic stub suggester; a `SetRecipeTagsAsync` seed helper; three scenarios.

Important — **build the SPA before running these** (the harness serves `ClientApp/build`):
```bash
cd Application/Frigorino.Web/ClientApp && npm run build
```

- [ ] **Step 1: Create the stub suggester**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free tag suggester for integration tests.
//   name contains "cake"/"kuchen" -> [Dessert, Baking]
//   everything else               -> [Main]
public sealed class StubRecipeTagSuggester : IRecipeTagSuggester
{
    public Task<IReadOnlyList<RecipeTag>> SuggestAsync(
        string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct)
    {
        var lower = name.ToLowerInvariant();
        IReadOnlyList<RecipeTag> tags =
            lower.Contains("cake") || lower.Contains("kuchen")
                ? new[] { RecipeTag.Dessert, RecipeTag.Baking }
                : new[] { RecipeTag.Main };
        return Task.FromResult(tags);
    }
}
```

- [ ] **Step 2: Register the stub in `TestWebApplicationFactory.cs`**

In `ConfigureServices`, alongside the existing `RemoveAll<IItemClassifier>()` / `RemoveAll<IQuantityExtractor>()` block, add:

```csharp
            // Deterministic, network-free recipe tag suggester (IRecipeTagSuggester is always
            // registered — real or Null — so RemoveAll + replace works regardless of AI config).
            services.RemoveAll<IRecipeTagSuggester>();
            services.AddScoped<IRecipeTagSuggester, StubRecipeTagSuggester>();
```

Ensure the usings include `using Frigorino.Domain.Interfaces;` (already present) and the stub's namespace (same `Frigorino.IntegrationTests.Infrastructure`).

- [ ] **Step 3: Add the seed helper to `TestApiClient.cs`**

Mirror the existing recipe helpers:

```csharp
public Task<IAPIResponse> TrySetRecipeTagsAsync(int recipeId, string[] tags, int? householdId = null)
{
    var targetHouseholdId = householdId ?? ctx.HouseholdId;
    return ctx.BrowserContext.APIRequest.PutAsync(
        $"/api/household/{targetHouseholdId}/recipes/{recipeId}/tags",
        new APIRequestContextOptions
        {
            DataObject = new { tags },
            Headers = AuthHeaders,
        });
}

public async Task SetRecipeTagsAsync(int recipeId, params string[] tags)
{
    var resp = await TrySetRecipeTagsAsync(recipeId, tags);
    if (!resp.Ok)
    {
        throw new Exception($"SetRecipeTagsAsync failed: {resp.Status} {await resp.TextAsync()}");
    }
}
```

- [ ] **Step 4: Write the feature file**

`Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeTags.feature`:

```gherkin
Feature: Recipe tags
  Recipes can be tagged with curated course/dietary tags, filtered on the overview,
  and offered AI tag suggestions on demand.

  Scenario: Tags set on a recipe persist and show on the view
    Given there is a recipe named "Margherita Pizza"
    And the recipe "Margherita Pizza" has tags "Main,Vegetarian"
    When I open the recipe "Margherita Pizza"
    Then the recipe view shows the tag "Main"
    And the recipe view shows the tag "Vegetarian"

  Scenario: Overview tag filter narrows the list
    Given there is a recipe named "Chicken Curry"
    And the recipe "Chicken Curry" has tags "Main"
    And there is a recipe named "Fruit Salad"
    And the recipe "Fruit Salad" has tags "Salad"
    When I open the recipes overview
    And I toggle the overview tag filter "Salad"
    Then the recipe "Fruit Salad" appears in the recipe overview
    And "Chicken Curry" no longer appears in the recipe overview

  Scenario: Suggest tags offers ghost chips the user can accept
    Given there is a recipe named "Carrot Cake"
    When I open the recipe "Carrot Cake" for editing
    And I tap suggest tags
    Then a suggested tag chip "Dessert" is shown
    When I accept the suggested tag "Dessert"
    Then the recipe tag "Dessert" is selected
```

- [ ] **Step 5: Write the step bindings**

`Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeTagSteps.cs` — reuse `RecipeSteps` phrasings where they already exist (`there is a recipe named`, `I open the recipe`, `I open the recipe … for editing`, `… no longer appears in the recipe overview`, `… appears in the recipe overview`). Add only the new, globally-unique step phrases below:

```csharp
using Frigorino.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Reqnroll;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeTagSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("the recipe {string} has tags {string}")]
    public async Task GivenTheRecipeHasTags(string recipeName, string csvTags)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var tags = csvTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await api.SetRecipeTagsAsync(recipeId, tags);
    }

    [When("I open the recipes overview")]
    public async Task WhenIOpenTheRecipesOverview()
    {
        await ctx.Page.GotoAsync("/recipes",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [When("I toggle the overview tag filter {string}")]
    public async Task WhenIToggleTheOverviewTagFilter(string tag)
    {
        await ctx.Page.GetByTestId($"recipe-filter-tag-{tag}").ClickAsync();
    }

    [Then("the recipe view shows the tag {string}")]
    public async Task ThenTheRecipeViewShowsTheTag(string tag)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-{tag}")).ToBeVisibleAsync();
    }

    [When("I tap suggest tags")]
    public async Task WhenITapSuggestTags()
    {
        await ctx.Page.GetByTestId("recipe-suggest-tags").ClickAsync();
    }

    [Then("a suggested tag chip {string} is shown")]
    public async Task ThenASuggestedTagChipIsShown(string tag)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-suggested-{tag}")).ToBeVisibleAsync();
    }

    [When("I accept the suggested tag {string}")]
    public async Task WhenIAcceptTheSuggestedTag(string tag)
    {
        // Await the PUT so the follow-up "selected" assertion sees a stable DOM.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/tags")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId($"recipe-tag-suggested-{tag}").ClickAsync();
        await responseTask;
    }

    [Then("the recipe tag {string} is selected")]
    public async Task ThenTheRecipeTagIsSelected(string tag)
    {
        // The selectable chip in the edit selector goes filled (MUI adds MuiChip-filled). Assert it
        // is present and visible; the accept flow removed it from the ghost row.
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-select-{tag}")).ToBeVisibleAsync();
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-suggested-{tag}")).Not.ToBeVisibleAsync();
    }
}
```

Note on global step uniqueness: if any of these phrases collide with an existing binding (Reqnroll throws `AmbiguousBindingException` at runtime), rename the new phrase. The phrases above are chosen to be distinct from `RecipeSteps`.

- [ ] **Step 6: Build the SPA, then run the new scenarios**

```bash
cd Application/Frigorino.Web/ClientApp && npm run build
cd ../../.. && dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeTags"
```
Expected: the three scenarios PASS. The round-trip scenario proves the `integer[]` EF mapping + projection end-to-end; if it fails on a DB error mentioning the column/array, revisit Task 3 Step 3.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.IntegrationTests/
git commit -m "test(recipes): integration coverage for tags (round-trip, filter, suggest)"
```

---

### Task 16: Documentation + full verification gate

**Files:**
- Modify: `knowledge/Recipes.md`, `knowledge/AI_Classification.md`, `CLAUDE.md`

- [ ] **Step 1: Update `knowledge/Recipes.md`**

- Domain table: add a `Tags` row to `Recipe.cs` (`List<RecipeTag>`, value-set, `integer[]` column, `SetTags` replace-whole-set, role-gated, capped at `MaxTags`).
- API surface: add `PUT /{id}/tags` and `POST /{id}/suggest-tags` (the latter noted as the synchronous on-demand AI exception).
- Key decisions: tags are a value-set not an aggregate child (no rank/soft-delete); ingredients stay in search, not tags.
- Frontend: edit selector + suggest ghost chips; view chips; overview tag filter (client-side AND with search).

- [ ] **Step 2: Update `knowledge/AI_Classification.md`**

- Add a section for the recipe tag suggester: synchronous, on-demand, stateless, suggest-only (no job/trigger/queue, unlike the others); `IRecipeTagSuggester` port + `OpenAiRecipeTagSuggester`/`NullRecipeTagSuggester`; gated on `Ai:RecipeTagSuggester:Enabled` + `Ai:ApiKey`; `AddRecipeTagSuggestion` DI.
- Add a chaining-summary row: recipe tag suggestion → nothing (writes no Product rows).

- [ ] **Step 3: Update `CLAUDE.md`**

- Add `AddRecipeTagSuggestion` to the DI-extension list in the architecture overview.
- Add `Ai:RecipeTagSuggester:*` to the configuration list.

- [ ] **Step 4: Commit docs**

```bash
git add knowledge/Recipes.md knowledge/AI_Classification.md CLAUDE.md
git commit -m "docs(recipes): document tags + AI tag suggester"
```

- [ ] **Step 5: Full backend + integration test run**

Run: `dotnet test Application/Frigorino.sln`
Expected: all pass (unit + integration). Read the IT log dump on any red before bisecting.

- [ ] **Step 6: Full frontend verification**

Run from `ClientApp/`: `npm run lint && npm run tsc && npm run prettier && npm run build`
Expected: all pass; `build/` regenerated.

- [ ] **Step 7: Docker build gate**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: succeeds (catches pipeline/SPA/Dockerfile drift). If the Docker daemon is unreachable, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 8: Final commit (if anything changed during verification)**

```bash
git add -A
git commit -m "chore(recipes): tag feature verification fixups"
```

---

## Self-Review

**Spec coverage** — every spec section maps to a task:
- Taxonomy (Course + Dietary, incl. LactoseFree) → Task 1.
- `Recipe.Tags` + `SetTags` (replace-set, dedupe, cap, role gate) → Task 2.
- `integer[]` storage + migration → Task 3.
- `RecipeResponse.Tags` projection → Task 4.
- `PUT …/tags` slice → Task 5.
- `IRecipeTagSuggester` + Null + DI + config + `Ai:RecipeTagSuggester:*` → Task 6.
- OpenAI adapter (Structured Outputs, enum-derived schema) → Task 7.
- `POST …/suggest-tags` synchronous slice → Task 8.
- Program.cs wiring + client regen → Task 9.
- `tags.ts` facet arrays + labels + i18n (en/de) → Task 10.
- `useSetRecipeTags` / `useSuggestRecipeTags` → Task 11.
- Read-only chips → Task 12.
- Edit selector + suggest + ghost chips → Task 13.
- View chips + overview AND filter → Task 14.
- Stub suggester + 3 IT scenarios (round-trip, filter, suggest) → Task 15.
- Docs (Recipes.md, AI_Classification.md, CLAUDE.md) → Task 16.
- Out-of-scope items (cuisine, custom tags, server-side filter, persisted suggestions, AI→Product chaining) → not implemented, by design.

**Type consistency** — `SetTags(string, HouseholdRole, IEnumerable<RecipeTag>)`, `Recipe.Tags: List<RecipeTag>`, `RecipeResponse.Tags: IReadOnlyList<RecipeTag>`, `IRecipeTagSuggester.SuggestAsync(...) -> Task<IReadOnlyList<RecipeTag>>`, `SuggestRecipeTagsResponse.SuggestedTags`, generated TS `RecipeTag` union, hooks spread `setRecipeTagsMutation`/`suggestRecipeTagsMutation` — names used consistently across tasks. Testids consistent: `recipe-tag-<Tag>` (read-only/view), `recipe-tag-select-<Tag>` (edit selector), `recipe-tag-suggested-<Tag>` (ghost), `recipe-filter-tag-<Tag>` (overview filter), `recipe-suggest-tags` (button).

**Known risk (flagged, not a placeholder):** the `List<RecipeTag>` → `integer[]` EF mapping (Task 3) is the one spot that can't be fully proven without running; Task 3 Step 3 and Task 15 Step 6 are the gates, with the exact migration shape to verify and a no-join-table fallback note.
