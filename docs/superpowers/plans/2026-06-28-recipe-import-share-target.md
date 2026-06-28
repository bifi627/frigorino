# Recipe import: PWA share-target + pre-import peek — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable pre-import "peek" (recipe name + image, shown before committing) to the existing recipe URL import, adopt it on the create page, and add a PWA share-target that lets a shared recipe link be peeked, assigned to a household, and imported.

**Architecture:** A new read-only `POST /api/recipes/import/preview` endpoint reuses `RecipeImportService.ImportAsync` (which already parses without persisting) and returns `{ name, imageUrl }`. A 2-minute in-memory cache in `ImportAsync` means the peek and the subsequent real import share a single network fetch. The frontend gets a `usePreviewRecipeImport` hook + a `RecipeImportPreviewCard`, consumed by both the refactored create-page form (peek → confirm) and a new `/recipes/import` receiver route (peek → household picker → confirm). A `share_target` manifest entry (GET) routes the OS share into the receiver; an `extractSharedUrl` helper pulls the URL out of the share's `text`/`url`/`title`.

**Tech Stack:** .NET 10 vertical slices + FluentResults; `Microsoft.Extensions.Caching.Memory`; React 19 + TanStack Router/Query + MUI; `vite-plugin-pwa`; Reqnroll + Playwright + Postgres Testcontainers; xUnit + FakeItEasy.

## Global Constraints

- **C# braces:** always block style `{ }`, even single-line conditions and namespaces.
- **Vertical slice shape:** one file per endpoint (request/response DTO + `Map*` extension + `Handle` colocated); reads project inline into the response DTO; no new controllers.
- **Enums on the wire:** string names (existing `JsonStringEnumConverter`) — not relevant here but don't change it.
- **API client is generated:** never hand-edit `src/lib/api/**`; regenerate with `npm run api` from `ClientApp/`. Generated files are committed.
- **API hooks:** never write `queryFn`/`mutationFn`/manual `queryKey`; spread the generated `*Options`/`*Mutation`; mutation hooks are arg-less (caller passes `{ path, body }`).
- **i18n:** keys added to **both** `public/locales/en/translation.json` and `de/translation.json`. New keys go under the **existing** `recipes.import` namespace → JSON only, no `i18next.d.ts` change. Tests never assert on translated text — assert on testids / dynamic data only.
- **Frontend tooling:** use `npm run tsc` / `npm run lint` / `npm run build` / `npm run api` — never raw `npx`. Run `npm run prettier` (write) before committing frontend changes.
- **DB-touching tests:** Reqnroll + Postgres Testcontainers in `Frigorino.IntegrationTests`; never SQLite/EF-InMemory.
- **IT serves `ClientApp/build`:** run `npm run build` after any React/route/manifest change or new testids won't appear.
- **Reqnroll step text is globally unique** across the whole IT assembly; new step phrases must not clash with existing bindings. **Filter ITs by scenario *title* words**, never the `.feature` file name; confirm `gesamt: N` ran.
- **Commits:** no `Co-Authored-By` / "Generated with" trailers. Conventional-commit style (`feat(recipes): …`).
- **Branch:** `feat/recipe-import-share-target` (already created off `stage`).

---

## Phase 1 — Backend: fetch cache + preview endpoint

### Task 1: Cache successful parses in `RecipeImportService`

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/RecipeImportService.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/RecipeImportDependencyInjection.cs`
- Test: `Application/Frigorino.Test/Infrastructure/RecipeImportServiceTests.cs`

**Interfaces:**
- Consumes: existing `RecipeImportService(HttpClient http)` ctor, `protected RecipeImportService()` test seam, `ImportAsync(string url, CancellationToken) → Result<ImportedRecipe>`.
- Produces: `RecipeImportService(HttpClient http, IMemoryCache? cache = null)` ctor; `ImportAsync` returns cached `ImportedRecipe` for a repeated URL within 2 minutes (successes only). `CreateDefault()` unchanged signature. `AddRecipeImport` now also calls `AddMemoryCache()`.

- [ ] **Step 1: Write the failing test**

Add to `RecipeImportServiceTests.cs` (inside the class), plus a counting handler:

```csharp
[Fact]
public async Task ImportAsync_caches_successful_parse_and_skips_second_fetch()
{
    const string html =
        "<html><head><script type=\"application/ld+json\">" +
        "{\"@context\":\"https://schema.org\",\"@type\":\"Recipe\",\"name\":\"Cake\",\"recipeIngredient\":[\"x\"]}" +
        "</script></head></html>";
    var handler = new CountingHandler(html);
    var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
        new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    var service = new RecipeImportService(new HttpClient(handler), cache);

    var first = await service.ImportAsync("https://example.com/r", CancellationToken.None);
    var second = await service.ImportAsync("https://example.com/r", CancellationToken.None);

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.Equal("Cake", second.Value.Name);
    Assert.Equal(1, handler.Count); // second call served from cache, no 2nd fetch
}

private sealed class CountingHandler(string html) : HttpMessageHandler
{
    public int Count { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Count++;
        var content = new StringContent(html, System.Text.Encoding.UTF8, "text/html");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeImportServiceTests.ImportAsync_caches_successful_parse"`
Expected: FAIL — `RecipeImportService` has no `(HttpClient, IMemoryCache)` constructor (compile error).

- [ ] **Step 3: Add the cache to the service**

In `RecipeImportService.cs`, add the using and field, a new constructor, and the cache read/write around the parse. Replace the constructor region and `ImportAsync`:

```csharp
using System.Net;
using FluentResults;
using Microsoft.Extensions.Caching.Memory;

namespace Frigorino.Infrastructure.Services
{
    public class RecipeImportService
    {
        public const long MaxResponseBytes = 15 * 1024 * 1024;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        private readonly HttpClient _http;
        // ponytail: 2-min in-memory cache keyed by URL, success-only; warms the peek→import double
        // call so only one network fetch happens. Add a size cap if import volume ever grows.
        private readonly IMemoryCache? _cache;

        public RecipeImportService(HttpClient http, IMemoryCache? cache = null)
        {
            _http = http;
            _cache = cache;
        }

        // ponytail: protected ctor is the IT test seam (StubRecipeImportService overrides ImportAsync);
        // avoids a one-impl interface that the spec deliberately omits.
        protected RecipeImportService()
        {
            _http = null!;
            _cache = null;
        }

        public static RecipeImportService CreateDefault() => new(BuildGuardedClient());
```

Then in `ImportAsync`, add a cache lookup at the top (after the `invalid_url` guard) and a cache write on success. Change the method to:

```csharp
        public virtual async Task<Result<ImportedRecipe>> ImportAsync(string url, CancellationToken ct)
        {
            if (!RecipeImportUrl.TryParseHttpUrl(url, out var uri))
            {
                return Fail("invalid_url", "Enter a valid http(s) URL.");
            }

            var cacheKey = $"recipe-import:{url.Trim()}";
            if (_cache is not null && _cache.TryGetValue(cacheKey, out ImportedRecipe? cached) && cached is not null)
            {
                return Result.Ok(cached);
            }

            string html;
            try
            {
                using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return Fail("fetch_failed", $"The page returned status {(int)resp.StatusCode}.");
                }
                var mediaType = resp.Content.Headers.ContentType?.MediaType;
                if (mediaType is null || !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    return Fail("fetch_failed", "The URL did not return an HTML page.");
                }
                if (resp.Content.Headers.ContentLength is > MaxResponseBytes)
                {
                    return Fail("page_too_large", "The page is too large to import.");
                }
                html = await ReadCappedAsync(resp, ct);
            }
            catch (ResponseTooLargeException)
            {
                return Fail("page_too_large", "The page is too large to import.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                return Fail("fetch_failed", "Could not fetch the page.");
            }

            var parsed = JsonLdRecipeParser.Parse(html);
            if (parsed is null)
            {
                return Fail("no_recipe_found", "Could not find a recipe on this page.");
            }

            _cache?.Set(cacheKey, parsed, CacheDuration);
            return Result.Ok(parsed);
        }
```

- [ ] **Step 4: Register the cache in DI**

In `RecipeImportDependencyInjection.cs`, add the memory cache and inject it into the singleton:

```csharp
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeImportDependencyInjection
    {
        // No config gate, no Null impl: the deterministic JSON-LD path has no vendor/API key — always on.
        public static IServiceCollection AddRecipeImport(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton(sp => new RecipeImportService(
                RecipeImportService.BuildGuardedClient(),
                sp.GetRequiredService<IMemoryCache>()));
            return services;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeImportServiceTests"`
Expected: PASS — all existing tests plus the new cache test (existing tests still construct `new RecipeImportService(StubClient(...))`, which binds to the new ctor with `cache` defaulting to null).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/RecipeImportService.cs Application/Frigorino.Infrastructure/Services/RecipeImportDependencyInjection.cs Application/Frigorino.Test/Infrastructure/RecipeImportServiceTests.cs
git commit -m "feat(recipes): cache parsed import results for 2 minutes"
```

---

### Task 2: Preview endpoint + API integration test + regenerated client

**Files:**
- Create: `Application/Frigorino.Features/Recipes/PreviewRecipeImport.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (after the recipes household group, ~line 447)
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportPreview.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportPreviewApiSteps.cs`
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/api/**` + `src/lib/openapi.json` via `npm run api`

**Interfaces:**
- Consumes: `RecipeImportService.ImportAsync` (Task 1), `ResultExtensions.ToValidationProblemResult()`.
- Produces: `POST /api/recipes/import/preview` `{ url }` → `200 { name, imageUrl }` / `400` (ValidationProblem on `Url` for `invalid_url`) / `422` (`{ ..., code }` for `no_recipe_found`/`page_too_large`/`fetch_failed`). Endpoint name `PreviewRecipeImport` → generated TS `previewRecipeImportMutation` + type `RecipeImportPreviewResponse { name: string; imageUrl?: string | null }`. New `TestApiClient.TryPreviewRecipeImportAsync(string url)`.

- [ ] **Step 1: Write the failing API test (feature + steps + client helper)**

Create `RecipeImportPreview.Api.feature`:

```gherkin
Feature: Recipe import preview API

  Background:
    Given I am logged in with an active household

  Scenario: Previewing a URL returns the recipe name
    When I preview the recipe URL "https://example.com/pancakes" via the API
    Then the API response status is 200
    And the preview response name is "Imported Pancakes"

  Scenario: Previewing a page with no recipe returns 422 with a no_recipe_found code
    When I preview the recipe URL "https://example.com/norecipe" via the API
    Then the API response status is 422
    And the API response has the import error code "no_recipe_found"
```

Create `RecipeImportPreviewApiSteps.cs` (reuses the existing `the API response status is {int}` and `the API response has the import error code {string}` bindings — define only the two new unique phrases):

```csharp
namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportPreviewApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I preview the recipe URL {string} via the API")]
    public async Task WhenIPreviewTheRecipeUrlViaTheApi(string url)
    {
        ctx.LastApiResponse = await api.TryPreviewRecipeImportAsync(url);
    }

    [Then("the preview response name is {string}")]
    public async Task ThenThePreviewResponseNameIs(string name)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        Assert.Equal(name, json.GetProperty("name").GetString());
    }
}
```

Add to `TestApiClient.cs` (next to `TryImportRecipeAsync`):

```csharp
    public Task<IAPIResponse> TryPreviewRecipeImportAsync(string url)
    {
        return ctx.BrowserContext.APIRequest.PostAsync(
            "/api/recipes/import/preview",
            new APIRequestContextOptions
            {
                Headers = AuthHeaders,
                DataObject = new { url },
            });
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeImportPreviewAPI"`
Expected: FAIL — endpoint not mapped (404 → status assertion fails), or compile error until the slice exists.

- [ ] **Step 3: Write the preview slice**

Create `PreviewRecipeImport.cs`:

```csharp
using FluentResults;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Recipes
{
    public sealed record PreviewRecipeImportRequest(string Url);

    public sealed record RecipeImportPreviewResponse(string Name, string? ImageUrl);

    public static class PreviewRecipeImportEndpoint
    {
        public static IEndpointRouteBuilder MapPreviewRecipeImport(this IEndpointRouteBuilder app)
        {
            app.MapPost("import/preview", Handle)
               .WithName("PreviewRecipeImport")
               .Produces<RecipeImportPreviewResponse>()
               .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
               .ProducesValidationProblem();
            return app;
        }

        // Read-only peek: fetch + parse only (no household scope, no persistence). Reuses the same
        // cache + error codes as the real import (ImportRecipe.cs); the real import re-runs ImportAsync
        // and hits the cache, so no double network fetch.
        private static async Task<Results<Ok<RecipeImportPreviewResponse>, ValidationProblem, ProblemHttpResult>> Handle(
            PreviewRecipeImportRequest request,
            RecipeImportService importService,
            CancellationToken ct)
        {
            var import = await importService.ImportAsync(request.Url, ct);
            if (import.IsFailed)
            {
                var code = import.Errors[0].Metadata.TryGetValue("code", out var c) ? c?.ToString() : null;
                if (code == "invalid_url")
                {
                    return new Error("Enter a valid http(s) URL.").WithProperty("Url").ToValidationProblemResult();
                }
                return TypedResults.Problem(
                    detail: import.Errors[0].Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["code"] = code });
            }

            return TypedResults.Ok(new RecipeImportPreviewResponse(import.Value.Name, import.Value.ImageUrl));
        }
    }
}
```

- [ ] **Step 4: Register the endpoint group**

In `Program.cs`, immediately after the recipes household group block (the `recipes.MapSuggestRecipeTags();` line, ~line 447), add:

```csharp
// Recipe import preview is identity-only (no household scope — the peek precedes household choice).
var recipeImport = app.MapGroup("/api/recipes")
    .RequireAuthorization()
    .WithTags("Recipes");
recipeImport.MapPreviewRecipeImport();
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeImportPreviewAPI"`
Expected: PASS — both scenarios. Confirm `gesamt: 2`.

- [ ] **Step 6: Regenerate the frontend API client**

Run: `cd Application/Frigorino.Web/ClientApp && npm run api`
Expected: `src/lib/openapi.json` gains the `/api/recipes/import/preview` path; `src/lib/api/@tanstack/react-query.gen.ts` gains `previewRecipeImportMutation`; `types.gen.ts` gains `RecipeImportPreviewResponse`.

Verify: `npx --no-install grep -r previewRecipeImportMutation src/lib/api/` — or just `npm run tsc` later will confirm the symbol exists.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/Recipes/PreviewRecipeImport.cs Application/Frigorino.Web/Program.cs Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportPreview.Api.feature Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportPreviewApiSteps.cs Application/Frigorino.Web/ClientApp/src/lib
git commit -m "feat(recipes): add read-only import preview endpoint"
```

---

## Phase 2 — Frontend: shared peek primitive + create-page adoption

### Task 3: Preview hook + shared error-message util

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/usePreviewRecipeImport.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/recipeImportError.ts`

**Interfaces:**
- Consumes: generated `previewRecipeImportMutation` (Task 2); existing `recipes.import.*` + `common.errorOccurred` i18n keys.
- Produces: `usePreviewRecipeImport()` → arg-less mutation; caller passes `{ body: { url } }`; `mutation.data` is `RecipeImportPreviewResponse | undefined`. `recipeImportErrorMessage(err: unknown, t: TFunction) => string`.

- [ ] **Step 1: Write the shared error util**

Create `recipeImportError.ts`:

```ts
import type { TFunction } from "i18next";

// Maps an import/preview error (hey-api throws the parsed response body on non-2xx) to a user message.
// 422 bodies carry a `code`; 400 ValidationProblem carries `{ errors: { Url: [...] } }` and no code.
export const recipeImportErrorMessage = (err: unknown, t: TFunction): string => {
    const code = (err as { code?: string } | null)?.code;
    if (code === "no_recipe_found") {
        return t("recipes.import.noRecipeFound");
    }
    if (code === "page_too_large") {
        return t("recipes.import.pageTooLarge");
    }
    if (code === "fetch_failed") {
        return t("recipes.import.fetchFailed");
    }
    const errors = (err as { errors?: Record<string, string[]> } | null)?.errors;
    if (errors && Object.keys(errors).length > 0) {
        return t("recipes.import.invalidUrl");
    }
    return t("common.errorOccurred");
};
```

- [ ] **Step 2: Write the preview hook**

Create `usePreviewRecipeImport.ts`:

```ts
import { useMutation } from "@tanstack/react-query";
import { previewRecipeImportMutation } from "../../lib/api/@tanstack/react-query.gen";

// Read-only peek of a recipe URL (name + image) before importing. Arg-less; caller passes
// { body: { url } } to mutate. No invalidation — it persists nothing.
export const usePreviewRecipeImport = () =>
    useMutation({ ...previewRecipeImportMutation() });
```

- [ ] **Step 3: Type-check**

Run: `cd Application/Frigorino.Web/ClientApp && npm run tsc`
Expected: PASS — `previewRecipeImportMutation` resolves (proves Task 2's regen worked).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/usePreviewRecipeImport.ts Application/Frigorino.Web/ClientApp/src/features/recipes/recipeImportError.ts
git commit -m "feat(recipes): add import preview hook + shared error mapper"
```

---

### Task 4: `RecipeImportPreviewCard` component

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeImportPreviewCard.tsx`
- Modify: `public/locales/en/translation.json` + `de/translation.json` (add `recipes.import.preview`)

**Interfaces:**
- Consumes: `recipeImportErrorMessage` (Task 3), `RecipeImportPreviewResponse` shape `{ name, imageUrl? }`.
- Produces: `<RecipeImportPreviewCard isPending isError error preview />`. Testids: `recipe-import-preview` (card), `recipe-peek-name` (name), `recipe-peek-image` (img), `recipe-import-error` (error alert — same testid the create-page error scenario already asserts).

- [ ] **Step 1: Add the i18n key (en then de)**

In `en/translation.json`, inside the `"import"` object, add after `"submit": "Import",`:

```json
            "preview": "Preview",
```

In `de/translation.json`, same spot:

```json
            "preview": "Vorschau",
```

- [ ] **Step 2: Write the component**

Create `RecipeImportPreviewCard.tsx`:

```tsx
import { Alert, Box, Card, CardContent, Skeleton, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { recipeImportErrorMessage } from "../recipeImportError";

interface RecipeImportPreview {
    name: string;
    imageUrl?: string | null;
}

interface RecipeImportPreviewCardProps {
    isPending: boolean;
    isError: boolean;
    error: unknown;
    preview: RecipeImportPreview | undefined;
}

// Pre-import peek: shows the parsed recipe name + image (rendered straight from the source URL —
// no server processing). Presentational only; the confirm controls live in the consumer.
export const RecipeImportPreviewCard = ({
    isPending,
    isError,
    error,
    preview,
}: RecipeImportPreviewCardProps) => {
    const { t } = useTranslation();

    if (isError) {
        return (
            <Alert severity="error" data-testid="recipe-import-error">
                {recipeImportErrorMessage(error, t)}
            </Alert>
        );
    }

    if (isPending) {
        return (
            <Card variant="outlined" data-testid="recipe-import-preview">
                <CardContent
                    sx={{ display: "flex", gap: 2, alignItems: "center" }}
                >
                    <Skeleton variant="rounded" width={64} height={64} />
                    <Skeleton variant="text" sx={{ flex: 1 }} />
                </CardContent>
            </Card>
        );
    }

    if (!preview) {
        return null;
    }

    return (
        <Card variant="outlined" data-testid="recipe-import-preview">
            <CardContent sx={{ display: "flex", gap: 2, alignItems: "center" }}>
                {preview.imageUrl ? (
                    <Box
                        component="img"
                        src={preview.imageUrl}
                        alt=""
                        data-testid="recipe-peek-image"
                        sx={{
                            width: 64,
                            height: 64,
                            objectFit: "cover",
                            borderRadius: 1,
                            flexShrink: 0,
                        }}
                        onError={(e) => {
                            (e.currentTarget as HTMLImageElement).style.display =
                                "none";
                        }}
                    />
                ) : null}
                <Typography
                    variant="subtitle1"
                    data-testid="recipe-peek-name"
                    sx={{ fontWeight: 600 }}
                >
                    {preview.name}
                </Typography>
            </CardContent>
        </Card>
    );
};
```

- [ ] **Step 3: Type-check + lint**

Run: `cd Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeImportPreviewCard.tsx Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat(recipes): add recipe import preview card"
```

---

### Task 5: Adopt peek→confirm on the create page

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/CreateRecipeForm.tsx`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportUiSteps.cs`

**Interfaces:**
- Consumes: `usePreviewRecipeImport` (Task 3), `RecipeImportPreviewCard` (Task 4), existing `useImportRecipe`/`useCreateRecipe`.
- Produces: create-page import is now peek (`recipe-import-submit`) → preview card → confirm (`recipe-import-confirm`). New IT steps `I confirm the import` (clicks `recipe-import-confirm`) and `the import preview shows {string}` (asserts `recipe-peek-name`).

- [ ] **Step 1: Update the IT feature + steps (the failing test)**

Replace `RecipeImport.feature` with:

```gherkin
Feature: Recipe import UI

  Background:
    Given I am logged in with an active household

  Scenario: Importing a URL from the create page lands on the edit page
    When I navigate to "/recipes/create"
    And I submit the import URL "https://example.com/pancakes"
    And the import preview shows "Imported Pancakes"
    And I confirm the import
    Then I am taken to the recipe edit page

  Scenario: A page with no recipe shows an inline error
    When I navigate to "/recipes/create"
    And I submit the import URL "https://example.com/norecipe"
    Then the import shows an error

  Scenario: A typed name takes precedence over the parsed recipe name
    When I navigate to "/recipes/create"
    And I fill in the recipe name "My Own Title"
    And I submit the import URL "https://example.com/pancakes"
    And the import preview shows "Imported Pancakes"
    And I confirm the import
    Then I am taken to the recipe edit page
    And the recipe name field shows "My Own Title"

  Scenario: An invalid URL keeps the import button disabled
    When I navigate to "/recipes/create"
    And I enter the import URL "not a url"
    Then the import submit is disabled
```

In `RecipeImportUiSteps.cs`, add two new step methods (keep all existing ones):

```csharp
    [When("the import preview shows {string}")]
    public async Task WhenTheImportPreviewShows(string name)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-peek-name")).ToHaveTextAsync(name);
    }

    [When("I confirm the import")]
    public async Task WhenIConfirmTheImport()
    {
        await ctx.Page.GetByTestId("recipe-import-confirm").ClickAsync();
    }
```

- [ ] **Step 2: Rewrite `CreateRecipeForm.tsx`**

Replace the file with (the manual form below the divider is unchanged from today; only the import form becomes peek→confirm and `messageFor` moves to the shared util):

```tsx
import { Add, Download } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Divider,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useCreateRecipe } from "../useCreateRecipe";
import { useImportRecipe } from "../useImportRecipe";
import { usePreviewRecipeImport } from "../usePreviewRecipeImport";
import { RecipeImportPreviewCard } from "./RecipeImportPreviewCard";

interface CreateRecipeFormProps {
    householdId: number;
}

// A non-empty value that parses as an http(s) URL. The backend revalidates — this is UX only.
const isValidHttpUrl = (value: string): boolean => {
    try {
        const u = new URL(value.trim());
        return u.protocol === "http:" || u.protocol === "https:";
    } catch {
        return false;
    }
};

export const CreateRecipeForm = ({ householdId }: CreateRecipeFormProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const createRecipeMutation = useCreateRecipe();
    const importRecipeMutation = useImportRecipe();
    const previewMutation = usePreviewRecipeImport();
    const [name, setName] = useState("");
    const [description, setDescription] = useState("");
    const [servings, setServings] = useState("");
    const [url, setUrl] = useState("");

    const isBusy =
        createRecipeMutation.isPending ||
        importRecipeMutation.isPending ||
        previewMutation.isPending;
    const createError: unknown = createRecipeMutation.error;
    const isInvalid = !name.trim() && name.length > 0;

    const trimmedUrl = url.trim();
    const urlInvalid = trimmedUrl.length > 0 && !isValidHttpUrl(trimmedUrl);
    const hasPreview = previewMutation.data !== undefined;
    const showPreview =
        previewMutation.isPending || previewMutation.isError || hasPreview;

    const handlePeek = (event: React.FormEvent) => {
        event.preventDefault();
        if (!isValidHttpUrl(trimmedUrl) || isBusy) {
            return;
        }
        previewMutation.mutate({ body: { url: trimmedUrl } });
    };

    const handleConfirmImport = async () => {
        if (!isValidHttpUrl(trimmedUrl) || isBusy) {
            return;
        }
        try {
            // Typed name/description (if any) win over the parsed page — the slice prefers them.
            const recipe = await importRecipeMutation.mutateAsync({
                path: { householdId },
                body: {
                    url: trimmedUrl,
                    name: name.trim() || null,
                    description: description.trim() || null,
                },
            });
            toast.success(t("recipes.import.success"));
            navigate({
                to: "/recipes/$recipeId/edit",
                params: { recipeId: String(recipe.id) },
                replace: true,
            });
        } catch {
            // Surfaced inline via importRecipeMutation.error below.
        }
    };

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!name.trim() || isBusy) {
            return;
        }
        try {
            const response = await createRecipeMutation.mutateAsync({
                path: { householdId },
                body: {
                    name: name.trim(),
                    description: description.trim() || null,
                    servings: servings === "" ? null : Number(servings),
                },
            });
            if (response?.id) {
                navigate({
                    to: "/recipes/$recipeId/edit",
                    params: { recipeId: response.id.toString() },
                    replace: true,
                });
            }
        } catch (err) {
            console.error("Failed to create recipe:", err);
        }
    };

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <Stack spacing={3}>
                    <form onSubmit={handlePeek}>
                        <Stack spacing={2}>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600 }}
                            >
                                {t("recipes.import.open")}
                            </Typography>

                            <Stack
                                direction="row"
                                spacing={1}
                                sx={{ alignItems: "flex-start" }}
                            >
                                <TextField
                                    fullWidth
                                    type="url"
                                    placeholder={t(
                                        "recipes.import.urlPlaceholder",
                                    )}
                                    value={url}
                                    onChange={(e) => setUrl(e.target.value)}
                                    disabled={isBusy}
                                    error={urlInvalid}
                                    helperText={
                                        urlInvalid
                                            ? t("recipes.import.invalidUrl")
                                            : ""
                                    }
                                    slotProps={{
                                        htmlInput: {
                                            "data-testid": "recipe-import-url",
                                        },
                                    }}
                                />
                                <Button
                                    type="submit"
                                    variant="outlined"
                                    disabled={
                                        !trimmedUrl || urlInvalid || isBusy
                                    }
                                    startIcon={
                                        previewMutation.isPending ? (
                                            <CircularProgress
                                                size={16}
                                                color="inherit"
                                            />
                                        ) : (
                                            <Download />
                                        )
                                    }
                                    sx={{ flexShrink: 0, mt: 0.5 }}
                                    data-testid="recipe-import-submit"
                                >
                                    {t("recipes.import.preview")}
                                </Button>
                            </Stack>

                            {showPreview ? (
                                <RecipeImportPreviewCard
                                    isPending={previewMutation.isPending}
                                    isError={previewMutation.isError}
                                    error={previewMutation.error}
                                    preview={previewMutation.data}
                                />
                            ) : null}

                            {hasPreview ? (
                                <Button
                                    variant="contained"
                                    onClick={handleConfirmImport}
                                    disabled={isBusy}
                                    startIcon={
                                        importRecipeMutation.isPending ? (
                                            <CircularProgress
                                                size={16}
                                                color="inherit"
                                            />
                                        ) : (
                                            <Download />
                                        )
                                    }
                                    data-testid="recipe-import-confirm"
                                >
                                    {importRecipeMutation.isPending
                                        ? t("recipes.import.importing")
                                        : t("recipes.import.submit")}
                                </Button>
                            ) : null}
                        </Stack>
                    </form>

                    <Divider>{t("recipes.import.orManually")}</Divider>

                    <form onSubmit={handleSubmit}>
                        <Stack spacing={3}>
                            {createError ? (
                                <Alert severity="error">
                                    {createError instanceof Error
                                        ? createError.message
                                        : t("common.errorOccurred")}
                                </Alert>
                            ) : null}

                            <Box>
                                <Typography
                                    variant="subtitle1"
                                    sx={{ fontWeight: 600, mb: 1 }}
                                >
                                    {t("common.name")} *
                                </Typography>
                                <TextField
                                    fullWidth
                                    value={name}
                                    onChange={(e) => setName(e.target.value)}
                                    disabled={isBusy}
                                    error={isInvalid}
                                    helperText={
                                        isInvalid
                                            ? t("recipes.recipeNameRequired")
                                            : ""
                                    }
                                    slotProps={{
                                        htmlInput: {
                                            "data-testid": "recipe-create-name",
                                        },
                                    }}
                                />
                            </Box>

                            <Box>
                                <Typography
                                    variant="subtitle1"
                                    sx={{ fontWeight: 600, mb: 1 }}
                                >
                                    {t("recipes.description")}
                                </Typography>
                                <TextField
                                    fullWidth
                                    multiline
                                    minRows={2}
                                    value={description}
                                    onChange={(e) =>
                                        setDescription(e.target.value)
                                    }
                                    disabled={isBusy}
                                    placeholder={t(
                                        "recipes.descriptionPlaceholder",
                                    )}
                                    slotProps={{
                                        htmlInput: { maxLength: 1000 },
                                    }}
                                />
                            </Box>

                            <Box>
                                <Typography
                                    variant="subtitle1"
                                    sx={{ fontWeight: 600, mb: 1 }}
                                >
                                    {t("recipes.servings")}
                                </Typography>
                                <TextField
                                    type="number"
                                    value={servings}
                                    onChange={(e) =>
                                        setServings(e.target.value)
                                    }
                                    disabled={isBusy}
                                    sx={{ width: 120 }}
                                    slotProps={{
                                        htmlInput: {
                                            min: 1,
                                            max: 99,
                                            "data-testid":
                                                "recipe-servings-input",
                                        },
                                    }}
                                />
                            </Box>

                            <Button
                                data-testid="recipe-create-submit-button"
                                type="submit"
                                variant="contained"
                                size="large"
                                disabled={isBusy || !name.trim()}
                                startIcon={
                                    createRecipeMutation.isPending ? (
                                        <CircularProgress
                                            size={20}
                                            color="inherit"
                                        />
                                    ) : (
                                        <Add />
                                    )
                                }
                                sx={{
                                    py: { xs: 1, sm: 1.25 },
                                    fontWeight: 600,
                                    mt: 2,
                                }}
                            >
                                {createRecipeMutation.isPending
                                    ? t("common.creating")
                                    : t("recipes.createRecipe")}
                            </Button>
                        </Stack>
                    </form>
                </Stack>
            </CardContent>
        </Card>
    );
};
```

- [ ] **Step 3: Type-check, lint, prettier, build the SPA**

Run: `cd Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint && npm run prettier && npm run build`
Expected: PASS; `build/` updated so the IT harness serves the new testids.

- [ ] **Step 4: Run the create-page import ITs**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeImportUI"`
Expected: PASS — all four scenarios. Confirm `gesamt: 4`.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/CreateRecipeForm.tsx Application/Frigorino.Web/ClientApp/build Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImportUiSteps.cs
git commit -m "feat(recipes): peek before confirming import on the create page"
```

---

## Phase 3 — Frontend: PWA share-target receiver

### Task 6: Manifest `share_target` + `extractSharedUrl` helper

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/vite.config.ts` (the `VitePWA` `manifest` object)
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/extractSharedUrl.ts`

**Interfaces:**
- Produces: manifest `share_target` (GET, action `/recipes/import`, params `url`/`text`/`title`). `extractSharedUrl({ url?, text?, title? }) => string | undefined` — first http(s) URL scanning url → text → title. (Verified by the receiver IT in Task 7; no JS test runner exists.)

- [ ] **Step 1: Add the manifest entry**

In `vite.config.ts`, inside the `manifest: { ... }` object, after the `icons: [ ... ],` array, add:

```ts
                // Web Share Target: a shared recipe page (GET) routes into the receiver, which
                // peeks + imports. Android/Chromium only — iOS Safari has no Share Target API.
                // Static action path → no VITE_ build arg involved (no Railway build-args concern).
                share_target: {
                    action: "/recipes/import",
                    method: "GET",
                    params: {
                        url: "url",
                        text: "text",
                        title: "title",
                    },
                },
```

- [ ] **Step 2: Write the helper**

Create `extractSharedUrl.ts`:

```ts
// Chrome on Android usually drops the shared page URL into `text` (often noisy, e.g.
// "Great recipe https://…"), not `url`. Scan url → text → title for the first http(s) URL.
const HTTP_URL = /\bhttps?:\/\/[^\s]+/i;

export const extractSharedUrl = (params: {
    url?: string;
    text?: string;
    title?: string;
}): string | undefined => {
    for (const candidate of [params.url, params.text, params.title]) {
        if (!candidate) {
            continue;
        }
        const match = candidate.match(HTTP_URL);
        if (match) {
            return match[0];
        }
    }
    return undefined;
};
```

- [ ] **Step 3: Type-check + lint**

Run: `cd Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/vite.config.ts Application/Frigorino.Web/ClientApp/src/features/recipes/extractSharedUrl.ts
git commit -m "feat(recipes): add share-target manifest entry + URL extractor"
```

---

### Task 7: `/recipes/import` receiver route + page + IT

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/routes/recipes/import.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/ImportRecipePage.tsx`
- Modify: `public/locales/en/translation.json` + `de/translation.json` (`recipes.import.shareTitle`, `recipes.import.chooseHousehold`)
- Modify (generated): `src/routeTree.gen.ts` (auto via `npm run build`)
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature` (add receiver scenario)

**Interfaces:**
- Consumes: `extractSharedUrl` (Task 6), `usePreviewRecipeImport` (Task 3), `RecipeImportPreviewCard` (Task 4), `useImportRecipe`, `useUserHouseholds` (`{ id, name }[]`), `useSetCurrentHousehold` (`{ body: { householdId } }`), `requireAuth`, `RequireHousehold`, `pageContainerSx`.
- Produces: route `/recipes/import` with `validateSearch → { sharedUrl?: string }`; single household → `recipe-import-confirm` button; multiple → `recipe-import-household` rows. Reuses IT steps `the import preview shows {string}` + `I confirm the import` (Task 5).

- [ ] **Step 1: Add the receiver scenario (the failing test)**

Append to `RecipeImport.feature`:

```gherkin
  Scenario: Sharing a recipe link opens the receiver and imports
    When I navigate to "/recipes/import?text=Great%20recipe%20https%3A%2F%2Fexample.com%2Fpancakes"
    And the import preview shows "Imported Pancakes"
    And I confirm the import
    Then I am taken to the recipe edit page
```

(No new step bindings — `the import preview shows`, `I confirm the import`, `I navigate to`, and `I am taken to the recipe edit page` all already exist.)

- [ ] **Step 2: Write the receiver page**

Create `pages/ImportRecipePage.tsx`:

```tsx
import {
    Alert,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    List,
    ListItemButton,
    ListItemText,
    Stack,
    Typography,
} from "@mui/material";
import { getRouteApi, useNavigate } from "@tanstack/react-router";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { pageContainerSx } from "../../../theme";
import { useUserHouseholds } from "../../households/useUserHouseholds";
import { useSetCurrentHousehold } from "../../me/activeHousehold/useSetCurrentHousehold";
import { RecipeImportPreviewCard } from "../components/RecipeImportPreviewCard";
import { useImportRecipe } from "../useImportRecipe";
import { usePreviewRecipeImport } from "../usePreviewRecipeImport";

const routeApi = getRouteApi("/recipes/import");

// Receiver for the PWA share-target: peek the shared URL, pick a household (skipped when there's
// only one), then import into it. The chosen household MUST become active before navigating to the
// edit page — that page resolves its household from useCurrentHousehold, so a non-active import
// would 404.
export const ImportRecipePage = () => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { sharedUrl } = routeApi.useSearch();
    const previewMutation = usePreviewRecipeImport();
    const importMutation = useImportRecipe();
    const setCurrentHousehold = useSetCurrentHousehold();
    const { data: households } = useUserHouseholds();
    const peekedRef = useRef(false);

    useEffect(() => {
        if (sharedUrl && !peekedRef.current) {
            peekedRef.current = true; // ref-guard: fire the peek once (StrictMode double-invokes effects)
            previewMutation.mutate({ body: { url: sharedUrl } });
        }
    }, [sharedUrl, previewMutation]);

    const isBusy = setCurrentHousehold.isPending || importMutation.isPending;
    const canImport = previewMutation.data !== undefined && !isBusy;

    const handleImport = async (targetHouseholdId: number) => {
        if (!sharedUrl || !canImport) {
            return;
        }
        try {
            await setCurrentHousehold.mutateAsync({
                body: { householdId: targetHouseholdId },
            });
            const recipe = await importMutation.mutateAsync({
                path: { householdId: targetHouseholdId },
                body: { url: sharedUrl },
            });
            navigate({
                to: "/recipes/$recipeId/edit",
                params: { recipeId: String(recipe.id) },
                replace: true,
            });
        } catch {
            // Surfaced inline via importMutation.error below.
        }
    };

    if (!sharedUrl) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error" data-testid="recipe-import-error">
                    {t("recipes.import.noRecipeFound")}
                    <Button
                        onClick={() => navigate({ to: "/recipes/create" })}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("recipes.createRecipe")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    const multiHousehold = (households?.length ?? 0) > 1;
    const firstHousehold = households?.[0];

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Stack spacing={3}>
                <Typography
                    variant="h5"
                    component="h1"
                    sx={{ fontWeight: 600 }}
                >
                    {t("recipes.import.shareTitle")}
                </Typography>

                <RecipeImportPreviewCard
                    isPending={previewMutation.isPending}
                    isError={previewMutation.isError}
                    error={previewMutation.error}
                    preview={previewMutation.data}
                />

                {importMutation.isError ? (
                    <Alert severity="error">
                        {t("common.errorOccurred")}
                    </Alert>
                ) : null}

                {multiHousehold ? (
                    <Card variant="outlined">
                        <CardContent>
                            <Typography variant="subtitle2" sx={{ mb: 1 }}>
                                {t("recipes.import.chooseHousehold")}
                            </Typography>
                            <List disablePadding>
                                {households?.map((h) => (
                                    <ListItemButton
                                        key={h.id}
                                        disabled={!canImport}
                                        onClick={() => handleImport(h.id!)}
                                        data-testid="recipe-import-household"
                                    >
                                        <ListItemText primary={h.name} />
                                        {isBusy ? (
                                            <CircularProgress size={16} />
                                        ) : null}
                                    </ListItemButton>
                                ))}
                            </List>
                        </CardContent>
                    </Card>
                ) : (
                    <Button
                        variant="contained"
                        size="large"
                        disabled={!canImport || !firstHousehold}
                        onClick={() =>
                            firstHousehold && handleImport(firstHousehold.id!)
                        }
                        startIcon={
                            isBusy ? (
                                <CircularProgress size={16} color="inherit" />
                            ) : undefined
                        }
                        data-testid="recipe-import-confirm"
                    >
                        {t("recipes.import.submit")}
                    </Button>
                )}
            </Stack>
        </Container>
    );
};
```

- [ ] **Step 3: Write the route shell**

Create `routes/recipes/import.tsx`:

```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { extractSharedUrl } from "../../features/recipes/extractSharedUrl";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { ImportRecipePage } from "../../features/recipes/pages/ImportRecipePage";

type ImportSearch = {
    sharedUrl?: string;
};

export const Route = createFileRoute("/recipes/import")({
    beforeLoad: requireAuth,
    validateSearch: (search: Record<string, unknown>): ImportSearch => ({
        sharedUrl: extractSharedUrl({
            url: typeof search.url === "string" ? search.url : undefined,
            text: typeof search.text === "string" ? search.text : undefined,
            title: typeof search.title === "string" ? search.title : undefined,
        }),
    }),
    component: () => (
        <RequireHousehold>
            <ImportRecipePage />
        </RequireHousehold>
    ),
});
```

- [ ] **Step 4: Add the two i18n keys (en then de)**

In `en/translation.json`, inside `"import"`, add:

```json
            "shareTitle": "Import recipe",
            "chooseHousehold": "Choose a household",
```

In `de/translation.json`, inside `"import"`, add:

```json
            "shareTitle": "Rezept importieren",
            "chooseHousehold": "Haushalt wählen",
```

- [ ] **Step 5: Type-check, lint, prettier, build (regenerates `routeTree.gen.ts`)**

Run: `cd Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint && npm run prettier && npm run build`
Expected: PASS; `routeTree.gen.ts` now includes `/recipes/import` and `getRouteApi("/recipes/import")` type-checks; `build/` updated.

- [ ] **Step 6: Run the receiver IT**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeImportUI"`
Expected: PASS — now 5 scenarios incl. the receiver. Confirm `gesamt: 5`.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/routes/recipes/import.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/ImportRecipePage.tsx Application/Frigorino.Web/ClientApp/src/routeTree.gen.ts Application/Frigorino.Web/ClientApp/public/locales Application/Frigorino.Web/ClientApp/build Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeImport.feature
git commit -m "feat(recipes): share-target receiver route with household picker"
```

---

## Phase 4 — Docs + full verification

### Task 8: Update docs, IDEAS backlog, and run the full gate

**Files:**
- Modify: `knowledge/Recipes.md` (URL import section)
- Modify: `IDEAS_Recipes.md` (remove the shipped share-target item; log the authGuard follow-up)

**Interfaces:** none (docs + verification only).

- [ ] **Step 1: Update `knowledge/Recipes.md`**

In the **URL import (JSON-LD)** section, after the existing bullets, add a short paragraph:

```markdown
- **Pre-import peek + cache.** `POST /api/recipes/import/preview` (`PreviewRecipeImport.cs`, auth-only, **non-household** group `/api/recipes`) reuses `RecipeImportService.ImportAsync` to return `{ name, imageUrl }` without persisting — the same error codes as `/import`. `ImportAsync` caches successful parses in `IMemoryCache` keyed by URL for **2 minutes** (success-only), so the peek and the subsequent real import share one network fetch. Two entry points consume the peek (`RecipeImportPreviewCard` + `usePreviewRecipeImport`): the **create page** (peek → confirm, replacing the old blind POST) and the **PWA share-target receiver** `/recipes/import` (peek → household picker → `setCurrentHousehold` → import). The share-target is a `share_target` manifest entry (GET, in `vite.config.ts`); `extractSharedUrl` pulls the URL out of the share's `text`/`url`/`title` (Android dumps it into `text`). Android/Chromium only — iOS Safari has no Web Share Target API.
```

- [ ] **Step 2: Update `IDEAS_Recipes.md`**

Remove the `## PWA share-target for recipe import` item (now shipped). Add an authGuard follow-up item:

```markdown
## authGuard drops search params on login redirect

- **Why:** `requireAuth` (`src/common/authGuard.ts`) redirects an unauthenticated user to `/auth/login` carrying only `location.pathname`, not the search string. Any route launched cold (e.g. the recipe share-target `/recipes/import?text=…`) while logged out loses its params after login. Rare for an installed PWA (normally already authed), but a real gap.
- **Sketch:** Make `requireAuth` carry `location.search` (or the full href) into the login `redirect`, and have the login page restore it. Cross-cutting — affects every protected route — so verify the existing `/auth/login` redirect handling round-trips search.
- **Impact / cost:** Small but broad; needs a careful pass over all `redirect`-consuming routes.
```

- [ ] **Step 3: Commit docs**

```bash
git add knowledge/Recipes.md IDEAS_Recipes.md
git commit -m "docs(recipes): document import peek + share-target; log authGuard gap"
```

- [ ] **Step 4: Full backend + IT gate**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS (Frigorino.Test + Frigorino.IntegrationTests). If an IT fails in the full run but passes filtered 1/1, it's shared-DB-state flakiness — re-run that scenario alone before treating it as a regression.

- [ ] **Step 5: Full frontend gate**

Run: `cd Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint && npm run prettier:check && npm run build`
Expected: PASS.

- [ ] **Step 6: Docker build (catches Dockerfile/pipeline drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: SUCCESS. (If the Docker daemon is unreachable, ask the user to start Docker Desktop rather than skipping.)

- [ ] **Step 7: Manual PWA verification (optional, not automatable)**

The `share_target` manifest entry and SW aren't covered by tests. To verify on a device: install the PWA (Android/Chromium), open a recipe page in the browser, Share → Frigorino, confirm the receiver opens with a peek and imports. Note any issue; the automated ITs already cover the receiver route + import logic.

---

## Self-Review

**Spec coverage:**
- Preview endpoint (auth-only, `/api/recipes`, `{name,imageUrl}`, same error codes) → Task 2. ✓
- 2-min success-only cache in `ImportAsync` → Task 1. ✓
- `usePreviewRecipeImport` + `RecipeImportPreviewCard` + shared error mapper → Tasks 3, 4. ✓
- Create-page peek→confirm (preserves typed-name precedence + manual path) → Task 5. ✓
- Manifest `share_target` (GET, `/recipes/import`, no VITE_ arg) + `extractSharedUrl` → Task 6. ✓
- Receiver route: validateSearch, auto-peek (StrictMode ref-guard), household picker (skip on 1), setCurrentHousehold-before-navigate → Task 7. ✓
- Tests: cache unit (Task 1), preview API IT (Task 2), create-page IT (Task 5), receiver IT (Task 7). Multi-household IT was *optional* in the spec — omitted here (single-household receiver path covered; the picker branch is simple list rendering). ✓
- Docs (`knowledge/Recipes.md`) + IDEAS cleanup + authGuard follow-up + iOS/double-fetch ceilings → Task 8 / documented. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"/"similar to Task N" — all code shown in full. ✓

**Type consistency:** `RecipeImportPreviewResponse { name, imageUrl }` (Task 2) ↔ `RecipeImportPreview { name, imageUrl? }` card prop (Task 4) ↔ `previewMutation.data` consumers (Tasks 5, 7). `previewRecipeImportMutation` (Task 2 regen) ↔ `usePreviewRecipeImport` (Task 3). `useUserHouseholds` items use `.id`/`.name` and `.id!` (mirrors `HouseholdSwitcher`). `useSetCurrentHousehold` called with `{ body: { householdId } }` (matches its definition). Testids `recipe-import-submit` (peek), `recipe-import-confirm` (confirm), `recipe-peek-name`, `recipe-import-error`, `recipe-import-preview`, `recipe-import-household` consistent across components and IT steps. ✓
