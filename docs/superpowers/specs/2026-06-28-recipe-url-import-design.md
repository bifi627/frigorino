# Recipe URL import (JSON-LD)

**Status:** Design approved (brainstorm) — ready for implementation plan
**Date:** 2026-06-28
**Branch:** `feat/recipe-url-import` (off `stage`)
**Source idea:** "Import a recipe from a URL (and later PDF/photo)" in `IDEAS.md`
**Predecessor specs:** `2026-06-14-recipes-feature-design.md` (MVP), `2026-06-15-recipe-source-links-design.md` (links), `2026-06-14-recipe-metadata-servings-design.md` (servings)
**Knowledge docs:** `knowledge/Recipes.md`, `knowledge/AI_Classification.md` (quantity-extraction contract)

## Summary

Creating a recipe today means typing every ingredient into the composer one line at a time. The
mobile-native fix is **import**: paste a recipe URL and have the app populate the recipe for review.

This MVP fetches the page server-side, reads its `schema.org/Recipe` **JSON-LD** (the structured
block most recipe sites already embed), and creates a recipe from it: **name, servings, description,
ingredients** (as items in the default section) plus the **source URL** as a `RecipeLink`. On success
the user lands on the **existing recipe edit page** to correct anything before it's "theirs."

**Deliberately small.** The data already lives on the page as structured JSON, so there is **no AI,
no `IRecipeImporter` interface, no config gate, no `Null` impl** — the import is a plain Infrastructure
service the slice calls directly (slices already depend on Infrastructure, e.g. `CreateRecipe` injects
`ApplicationDbContext`). **No data-model change and no migration.** The whole feature is one slice, one
Infrastructure service (fetch + parse), one frontend import sheet, and reuse of existing aggregate
methods. The AI fallback, share-target, cover-image fetch, instructions storage, and broader
schema.org alignment are split out as separate follow-ups in `IDEAS_Recipes.md`.

## Scope

In scope:
1. `RecipeImportService` (Infrastructure, concrete) — `Task<Result<ImportedRecipe>> ImportAsync(url, ct)`:
   hardened server-side fetch (**SSRF-guarded**) + tolerant JSON-LD parse.
2. `AddRecipeImport` DI extension wiring a named, hardened `HttpClient` + the service (no config gate).
3. `ImportRecipe` slice — `POST …/recipes/import`, body `{ url }` → `201` `RecipeResponse`, reusing
   `Recipe.Create` / `AddSection` / `AddItem` / `AddLink`; fires the recipe quantity-extraction
   trigger per imported item.
4. Frontend: `useImportRecipe` hook + an "Import from URL" affordance and dialog on the recipes
   overview; on success, toast + redirect to the edit page.
5. i18n keys (en + de) under the existing `recipes` namespace; regenerated TS client.
6. Unit tests (JSON-LD parser + SSRF IP classifier) and an integration test (stubbed fetcher → SPA).

Out of scope (separate follow-ups — see `IDEAS_Recipes.md` → "Recipe URL import — deferred follow-ups"):
- **AI fallback** when no JSON-LD is present (the `IRecipeImporter` port + config gate land *there*).
- **Cooking instructions / steps** — `recipeInstructions` is dropped; the model has nowhere to store it.
- **PWA share-target** entry point.
- **Cover-image fetch** from JSON-LD `image`.
- **Broader schema.org/Recipe data-model alignment** (prep/cook time, nutrition, ingredient groups, export).
- PDF / photo import (a separate, larger track).
- Rate-limiting the outbound fetch (authenticated, household-scoped; the SSRF guard handles the
  dangerous case — revisit if abused).

---

## Data model

**None.** No new entity, no column, no migration. Import composes existing aggregate methods:
`Recipe.Create(name, description, householdId, userId, servings)` → `AddSection(null, null)` →
`AddItem(sectionId, ingredient, quantity: null, comment: null)` per ingredient → `AddLink(url, label)`.

`ImportedRecipe` is a plain Infrastructure record returned by the service — **not** an entity:

```
record ImportedRecipe(string Name, string? Description, int? Servings, IReadOnlyList<string> Ingredients, string? SourceName);
```

## Infrastructure — `RecipeImportService` (`Frigorino.Infrastructure/Services/`)

One public method, two private concerns (fetch, parse). Returns `FluentResults.Result<ImportedRecipe>`;
failures carry a stable `code` in error metadata (`.WithMetadata("code", "...")`) that the slice maps
to an HTTP status. Codes: `invalid_url`, `fetch_failed`, `no_recipe_found`.

### Fetch + SSRF hardening (the security boundary — not simplified away)

A **named `HttpClient`** (`"RecipeImport"`) over a `SocketsHttpHandler`. The load-bearing defense is a
`ConnectCallback` that only ever connects to an IP it has **validated as public**:

- On connect, resolve the host with `Dns.GetHostAddressesAsync`. If **any** resolved address is
  non-public, fail the connection (don't connect to a mixed-record host). Otherwise open the socket
  **directly to a validated `IPAddress`** — never re-resolve — closing the DNS-rebinding (TOCTOU)
  window. Because every connection (including each redirect hop) runs this callback, redirect-to-private
  is covered by the same hook. `// ponytail: ConnectCallback IP check is the load-bearing SSRF defense`.
- "Non-public" (rejected) = loopback (`IPAddress.IsLoopback`), private v4 (`10/8`, `172.16/12`,
  `192.168/16`), CGNAT (`100.64/10`), link-local (`169.254/16` incl. cloud metadata `169.254.169.254`;
  `fe80::/10`), unique-local v6 (`fc00::/7`), unspecified (`0.0.0.0`, `::`), and IPv4-mapped-IPv6
  equivalents of any of these. Pure helper `RecipeImportUrl.IsPublicIpAddress(IPAddress)` — unit-tested.
- Before fetching: parse the URL; require `Uri.TryCreate(..., Absolute)` with scheme ∈ {`http`,`https`}.
  Malformed / non-http(s) → `invalid_url` (this is pure input validation, safe to report verbatim).
- Handler/client knobs: `AllowAutoRedirect = true`, `MaxAutomaticRedirections = 5`, request timeout
  ~10s, send a normal browser `User-Agent` + `Accept: text/html`.
- Response guards → `fetch_failed`: non-success status; `Content-Type` not HTML; body over a **3 MB**
  cap (read with a bounded stream / check `Content-Length` then enforce while reading — never buffer
  an unbounded body); network/timeout exceptions.
- **A blocked (private-IP) target also surfaces as `fetch_failed`, not a distinct code** — bundling it
  avoids handing an attacker an SSRF oracle that confirms internal hostnames resolve. (Trade-off noted;
  acceptable for an authenticated, household-scoped app.)

### Parse — `JsonLdRecipeParser` (pure, unit-tested)

Separate pure class taking the HTML string, returning `ImportedRecipe?`:

- Extract `<script type="application/ld+json">…</script>` blocks via regex (Singleline, tolerant of
  attribute order/quoting). **No new dependency.** `// ponytail: regex extraction — swap to AngleSharp if it proves fragile`.
- Parse each block with `System.Text.Json` (`JsonDocument`). Tolerantly locate the Recipe node:
  top-level can be a single object, an array of objects, or an object with an `@graph` array; `@type`
  may be the string `"Recipe"` or an array containing `"Recipe"` (case-insensitive). Take the first match.
- Map fields:
  - `name` → required; `WebUtility.HtmlDecode` + trim. Missing/empty ⇒ no recipe (return `null`).
  - `description` → `HtmlDecode`, strip HTML tags, trim, truncate to `Recipe.DescriptionMaxLength` (1000).
  - `recipeYield` → first integer found (handles `4`, `"4 servings"`, `"Serves 4"`, `["4 servings","4"]`);
    clamp to `1..Recipe.ServingsMax` (99), else `null`.
  - `recipeIngredient[]` (preferred) or legacy `ingredients` → `HtmlDecode` + trim each, drop empties,
    truncate each to `RecipeItem.TextMaxLength`, **cap at 100** ingredients (overflow dropped).
  - `author`/site name → optional `SourceName` for the link label (best-effort; `null` is fine).
- Return `null` if no Recipe node, no `name`, or **zero** ingredients (an ingredient-less import is
  useless → the slice maps `null` to `no_recipe_found`).
- `recipeInstructions` is **read but discarded** (no storage; see out-of-scope).

### DI — `AddRecipeImport(this IServiceCollection)`

New extension in a `RecipeImportDependencyInjection.cs`, called from `Program.cs` (alongside the other
`Add*` infra extensions). Registers the named `HttpClient` with the hardened handler and
`RecipeImportService` (+ the pure `JsonLdRecipeParser`). **No config, no enabled-flag** — always on.

## API slice (`Frigorino.Features/Recipes/ImportRecipe.cs`)

```
POST /api/household/{householdId:int}/recipes/import     body: { "url": string }   → 201 RecipeResponse
```

Registered in `Program.cs` on the existing `recipes` group (`recipes.MapImportRecipe()`).

Handler (mirrors `CreateRecipe`'s shape):
1. `FindActiveMembershipWithUserAsync(householdId, userId)` → `404` if not a member.
2. `await importService.ImportAsync(request.Url, ct)`. On failure, map the error's `code` metadata:
   - `invalid_url` → **400** `ValidationProblem` (field `url`).
   - `fetch_failed` → **422** `UnprocessableEntity` with a `ProblemDetails` carrying
     `extensions["code"] = "fetch_failed"`.
   - `no_recipe_found` → **422** with `extensions["code"] = "no_recipe_found"`.
3. On success: `Recipe.Create(imported.Name, imported.Description, householdId, userId, imported.Servings)`
   (truncation upstream means this should pass; a failure still maps to `ValidationProblem`). Set
   `CreatedByUser`. `AddSection(null, null)`. For each ingredient `AddItem(section.Id, text, null, null)`.
   `AddLink(request.Url, imported.SourceName)`. `db.Recipes.Add(recipe)` → `SaveChangesAsync`.
4. **After save**, for each created item: route via `Quantities.ItemTextRouter.Analyze(text)` and fire
   `IRecipeQuantityExtractionTrigger.OnItemRouted(...)` so quantities extract asynchronously, exactly
   like the manual item-create slice. **This is the easy-to-forget contract** — import is a bulk add and
   carries the same obligation (`knowledge/AI_Classification.md`; no central enforcement). Recipe items
   never chain into product classification (by design).
5. Return `TypedResults.Created($"/api/household/{householdId}/recipes/{recipe.Id}", RecipeResponse.From(recipe, creator, itemCount))`.

Return type: `Results<Created<RecipeResponse>, NotFound, ValidationProblem, ProblemHttpResult>`.

Result codes returned to the SPA: `201` (ok), `400` (invalid url / validation), `404` (not a member),
`422` (`fetch_failed` | `no_recipe_found`). The `422` `code` extension is the SPA's branch key.

## Frontend (`ClientApp/src/features/recipes/`)

### Hook — `useImportRecipe.ts`

Arg-less mutation, spreads the generated `importRecipeMutation()`. Caller passes
`{ path: { householdId }, body: { url } }` to `mutateAsync`. `onSuccess` invalidates the
household-recipes list key (`getHouseholdRecipesQueryKey` equivalent) so the new recipe appears on
return. No optimistic update (foreground action; the user is navigating away).

### Entry point + dialog

- An **"Import from URL"** action on `pages/RecipesPage.tsx` near the create affordance
  (testid `recipe-import-open`).
- `components/ImportRecipeSheet.tsx` — MUI `Dialog`: a URL `TextField` (testid `recipe-import-url`),
  an Import button (testid `recipe-import-submit`, shows a spinner / disabled while pending), and an
  inline error area (testid `recipe-import-error`) that maps the failure to an i18n message:
  - `400`/invalid url → `recipes.import.invalidUrl`
  - `422` `fetch_failed` → `recipes.import.fetchFailed`
  - `422` `no_recipe_found` → `recipes.import.noRecipeFound`
  - anything else → `common.errorOccurred`
  Read the code defensively: `(error as { code?: string } | null)?.code` (hey-api throws the parsed
  body; type the local as `unknown`).
- On success: success toast (`recipes.import.success`) + `navigate({ to: "/recipes/$recipeId/edit",
  params: { recipeId } })` using the returned recipe id. The edit page's normal autosave handles the
  "review and correct" step — no new review screen.

### Client config

Regenerate the TS client (`npm run api`) after the slice lands.

## Cross-cutting

### i18n (`public/locales/{en,de}/translation.json`, under existing `recipes`)

New nested `recipes.import` keys: `open` ("Import from URL"), `title`, `urlLabel`, `urlPlaceholder`,
`submit`, `importing`, `success`, `invalidUrl`, `fetchFailed`, `noRecipeFound`. Existing namespace →
**JSON only**, no `src/types/i18next.d.ts` change needed.

### Testing

- **Unit (`Frigorino.Test`)** — pure logic, no DB, no network:
  - `JsonLdRecipeParser`: real-world samples — single object, `@graph`, top-level array, `@type` as
    array, `recipeYield` variants, legacy `ingredients`, HTML entities in name/description, >100
    ingredients (capped), and no-Recipe / no-name / no-ingredients → `null`. (The parser's runnable check.)
  - `RecipeImportUrl.IsPublicIpAddress`: loopback / private / CGNAT / link-local (incl.
    `169.254.169.254`) / ULA / unspecified / IPv4-mapped variants rejected; ordinary public v4/v6 allowed.
- **Integration (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Testcontainers)** — the SSRF
  guard blocks localhost, so a real outbound fetch in-test is impractical and flaky. **Register a stub
  `RecipeImportService`** in the IT host returning a fixed `ImportedRecipe`; drive the SPA: open the
  import sheet → submit a URL → assert redirect to the edit page and that the imported name + ingredient
  items render. A second scenario stubs a failure (e.g. `no_recipe_found`) and asserts the inline error
  shows. Steps assert on **testids / data-attributes only**, never translated text. New step phrases
  must be globally unique across the IT assembly; reused steps under different keywords get
  `[Given]`+`[When]` double-decoration (this repo's Reqnroll is keyword-sensitive).
- No frontend JS test runner exists; UI behavior is covered by the Playwright IT.

### Verification gate

`npm run tsc` + `npm run lint` + `npm run prettier` (write) + `npm run build` + full
`dotnet test Application/Frigorino.sln` (Test + IntegrationTests) + `docker build`. Run `npm run build`
before the IT (the harness serves `ClientApp/build`). No Dockerfile change (no new project).

## Decisions / defaults

- **JSON-LD only, no AI** — the data is already structured on the page; the AI fallback is a separate
  follow-up. Sites without JSON-LD get a clean `no_recipe_found` ("couldn't read this page, add it
  manually"), not a 500.
- **No `IRecipeImporter` interface / no config gate** — the deterministic path has no vendor and no
  API key; the abstraction the IDEA proposed belongs with the (deferred) AI fallback.
- **Instructions dropped, source kept as a link** — the recipe model has no steps field today.
- **Save-then-edit** — import creates a real saved recipe and redirects to the existing edit page;
  "review" is normal editing (a bad import is one delete-tap). No preview/confirm screen.
- **Paste-URL only** — the PWA share-target is a fast-follow that reuses this same endpoint.
- **Ingredients flow through the existing async quantity extraction** — no special quantity parsing on
  import; "2 cups flour" is enriched exactly like a manually typed item.
- **Blocked (private-IP) targets report as `fetch_failed`** — no distinct code, to avoid an SSRF oracle.
- **Caps:** 3 MB response, 5 redirects, ~10s timeout, 100 ingredients, description 1000 chars — all
  to bound a server-side fetch on user-supplied input.
