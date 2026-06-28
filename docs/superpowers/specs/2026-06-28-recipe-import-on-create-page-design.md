# Recipe import on the create page — prefill precedence + meaningful errors — Design

**Date:** 2026-06-28
**Status:** Approved (pending spec review)
**Scope:** Recipe URL import only. Builds on the URL-import MVP (`2026-06-28-recipe-url-import-design.md`). The import *engine* (fetch + SSRF guard + JSON-LD parse + slice) is reused as-is; this change relocates the entry point, adds a precedence rule, splits one error code, and adds frontend URL validation.

## Problem

The URL-import MVP put import as a `Download` action on the recipes **overview**: paste URL → the slice creates the recipe (ingredients + source link) server-side → land on the edit page. After first use the feedback was:

1. **Wrong home.** Import belongs on the **create recipe page**, next to manual entry — not as a separate overview action.
2. **No precedence.** If the user has already typed a title/description, those should win over whatever the page parses to. Import should feel like *prefill*, not a separate create path.
3. **Misleading errors.** A page over the size cap reports "Couldn't reach that page" (it was reached — it's just too big). The two legitimate failure cases — *page too large* and *no recipe on the page* — need distinct, meaningful messages.
4. **No frontend URL validation.** A malformed URL round-trips to the server before the user learns it's invalid.

## Decisions (locked during brainstorming)

1. **Flow: create immediately, reuse today's engine** (not parse-then-prefill). Import still creates the recipe + ingredients + link in one call and lands on the edit page. Rejected alternative: a parse-only endpoint that returns a payload and defers creation until Save — bigger change (frontend ingredient preview/edit + create-on-save re-implements what the slice already does server-side) for a review step the edit page already provides.
2. **Precedence: backend.** `ImportRecipeRequest` gains optional `Name`/`Description`; the slice prefers them over the parsed values. One place owns precedence.
3. **Single entry point.** The overview `Download` action is **removed** — the create page is one tap away via `+`.
4. **Import UI merges into `CreateRecipeForm`** (not a separate component). The form already owns `name`/`description` state, so precedence is a direct read of the same state. The `ImportRecipeSheet` dialog is deleted.
5. **Size cap raised to 15 MB** (was 8 MB).
6. **Servings override is out of scope** — the ask was title/description only; parsed servings still applies. Stays in `IDEAS_Recipes.md`.

## (1) Page layout — `CreateRecipeForm.tsx`

The create page (`CreateRecipePage` → `CreateRecipeForm`) gains an inline import section at the top of the form:

```
Import from URL                  ← heading, reuses recipes.import.open
[ https://…           ] [Import] ← inline, always visible; Enter submits
──── or fill in manually ────    ← new key recipes.import.orManually
Name        [____________]       ← typed value wins over parsed
Description [____________]       ← typed value wins over parsed
Servings    [__]
            [ Create Recipe ]
```

- **Import** button → `useImportRecipe` with `{ url, name, description }` (typed name/description passed through; blank → omitted) → on success `toast.success` → `navigate` to `/recipes/$recipeId/edit`. Mirrors the dialog's current success path.
- **Create Recipe** button → `useCreateRecipe` (unchanged) → navigate to edit.
- While **either** mutation is pending, **both** buttons are disabled.
- **Error separation:** import errors (code-mapped, see §3) render in the import section; create errors render in the manual section. Distinct concerns, distinct slots — no shared error box.
- Testids: keep `recipe-import-url` and `recipe-import-submit` (now on the inline field/button). Drop `recipe-import-open` (the field is always visible — nothing to open). Keep `recipe-create-submit-button` and `recipe-servings-input`.

The import section is the form's first child; `name`/`description`/`servings` and the create button follow as today.

## (2) Precedence — `ImportRecipe.cs`

```csharp
public sealed record ImportRecipeRequest(string Url, string? Name = null, string? Description = null);
```

After the service returns `imported`, the slice computes effective values before `Recipe.Create`:

```csharp
var effectiveName = string.IsNullOrWhiteSpace(request.Name) ? imported.Name : request.Name.Trim();
var effectiveDescription = string.IsNullOrWhiteSpace(request.Description) ? imported.Description : request.Description.Trim();
var creation = Recipe.Create(effectiveName, effectiveDescription, householdId, currentUser.UserId, imported.Servings);
```

The source link still uses `imported.SourceName` for its label (unchanged). (A successful import always carries a name — the parser skips name-less nodes — so the override simply replaces a present value; it cannot rescue a nameless page.)

`Name`/`Description` are optional with defaults, so existing callers and the IT stub contract are source-compatible. The generated TS client picks up the two optional body fields on `npm run api`.

## (3) Meaningful errors — split out `page_too_large`

Today both size-cap paths in `RecipeImportService.ImportAsync` return `fetch_failed`:

- the `Content-Length is > MaxResponseBytes` pre-check, and
- the streaming cap in `ReadCappedAsync` (throws `IOException`, caught by the generic `catch` → "Could not fetch the page.").

Introduce a dedicated code `page_too_large` for both:

- Content-Length path → `Fail("page_too_large", "The page is too large to import.")`.
- Streaming path → throw a private `sealed class ResponseTooLargeException : IOException` from `ReadCappedAsync`, caught by a **new** `catch (ResponseTooLargeException)` placed **before** the existing generic `catch` → `Fail("page_too_large", …)`.

The slice already forwards any error code into `extensions["code"]` (no slice change). Raise `MaxResponseBytes` to `15 * 1024 * 1024`.

**Error contract** (codes → HTTP → message key):

| code | HTTP | frontend message key |
|------|------|----------------------|
| `invalid_url` | 400 ValidationProblem (`Url`) | `recipes.import.invalidUrl` — "Enter a valid recipe URL (http or https)." |
| `page_too_large` | 422 | **new** `recipes.import.pageTooLarge` — "This page is too large to import. Add the recipe manually." |
| `fetch_failed` | 422 | `recipes.import.fetchFailed` — "Couldn't reach that page…" (HTTP status / non-HTML / network) |
| `no_recipe_found` | 422 | `recipes.import.noRecipeFound` — "Couldn't read a recipe from that page…" (kept — already meaningful) |

Frontend `messageFor` gains the `page_too_large` → `pageTooLarge` branch. The existing `fetch_failed`/`no_recipe_found`/validation branches are unchanged.

## (4) Frontend URL validation

A native helper, no dependency:

```ts
const isValidHttpUrl = (value: string): boolean => {
    try {
        const u = new URL(value.trim());
        return u.protocol === "http:" || u.protocol === "https:";
    } catch {
        return false;
    }
};
```

- Import button disabled when the URL is empty **or** `!isValidHttpUrl(url)`.
- When the field is non-empty and invalid, show an inline validation message (`recipes.import.invalidUrl`, reusing the existing key) under the field — distinct from a server error Alert.
- **Enter-to-submit:** wrap the URL field so Enter triggers import (an `onKeyDown` on the field, or a `<form>` around the import section with `onSubmit`). Guard the same valid/pending conditions as the button.
- The backend keeps `TryParseHttpUrl` — defense in depth; the frontend check is UX, not the security boundary.

## (5) Cleanup (dead code)

- Delete `ImportRecipeSheet.tsx`.
- `RecipesPage.tsx`: remove the `Download` `directAction`, the `ImportRecipeSheet` import + render, and the `importOpen` state.
- `recipes.import.open` ("Import from URL") is repurposed as the inline section heading — no longer dead.
- `recipe-import-open` testid removed.

## (6) i18n

Existing `recipes.import.*` namespace, so **JSON only** (no `i18next.d.ts` change — that's only for brand-new top-level namespaces). Add to `en` and `de`:

- `recipes.import.orManually` — "or fill in manually" / "oder manuell eingeben".
- `recipes.import.pageTooLarge` — en: "This page is too large to import. Add the recipe manually." / de: "Diese Seite ist zu groß zum Importieren. Füge das Rezept manuell hinzu."

`recipes.import.open` already exists in both files.

## Testing

- **Unit (`RecipeImportService`):** `page_too_large` on the Content-Length path; `page_too_large` on the streaming-cap path (response with no/under-stated Content-Length but body over cap). Mirror the existing service test setup.
- **Unit / slice:** precedence — when the request supplies `Name`/`Description`, the created recipe uses them, not the parsed values; when blank, parsed values apply.
- **IT (Playwright):** re-point `RecipeImport.feature` / `RecipeImportUiSteps` at the create page — navigate to `/recipes/create`, fill `recipe-import-url`, click `recipe-import-submit`, assert arrival on the edit page. Add: a typed-name-precedence scenario (type a name, import, assert the edit page shows the typed name) and an invalid-URL scenario (frontend disables submit / shows validation, no network call). The stub (`StubRecipeImportService`) contract is unchanged; precedence is exercised through the real slice.

## Out of scope (stays in `IDEAS_Recipes.md`)

Servings override, richer schema.org section mapping, AI fallback for non-JSON-LD sites, cover-image import. The head-bar `aria-label` smell is parked — the `Download` button it concerned is being removed.

## Files touched

- `Application/Frigorino.Infrastructure/Services/RecipeImportService.cs` — 15 MB cap, `page_too_large` split, `ResponseTooLargeException`.
- `Application/Frigorino.Features/Recipes/ImportRecipe.cs` — request DTO + precedence.
- `Application/Frigorino.Web/ClientApp/src/features/recipes/components/CreateRecipeForm.tsx` — inline import section, validation, Enter-to-submit, error separation.
- `Application/Frigorino.Web/ClientApp/src/features/recipes/components/ImportRecipeSheet.tsx` — deleted.
- `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipesPage.tsx` — remove overview action.
- `Application/Frigorino.Web/ClientApp/public/locales/{en,de}/translation.json` — `orManually`, `pageTooLarge`.
- `Application/Frigorino.Web/ClientApp/src/features/recipes/useImportRecipe.ts` — only if the hook needs the body shape widened (codegen handles the optional fields; likely no change).
- Tests: `RecipeImportService` unit, precedence unit/slice, `RecipeImport.feature` + `RecipeImportUiSteps.cs`.
- `knowledge/Recipes.md` — update the URL-import flow subsection (entry point moved, precedence, error codes, 15 MB).
- `IDEAS_Recipes.md` — remove the now-implemented "Import entry point on the create page" item.
