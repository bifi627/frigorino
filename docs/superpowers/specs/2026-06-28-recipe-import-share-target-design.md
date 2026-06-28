# Recipe import: PWA share-target + pre-import peek

**Date:** 2026-06-28
**Status:** Design approved, ready for implementation plan
**Branch:** `feat/recipe-import-share-target`

## Summary

Add two things on top of the existing JSON-LD recipe import engine:

1. A **pre-import peek** — a read-only "here's what we found" step (recipe name +
   image) shown before the import is committed. Built as a reusable primitive and
   adopted on the existing **create-page** import flow (so it's testable in a normal
   browser, no PWA needed).
2. A **PWA share-target** — share a recipe page from the phone's share sheet →
   Frigorino opens → peek → pick a household (skipped when the user has only one) →
   import → land on the editable recipe.

No changes to the parse/persist engine itself. The peek reuses
`RecipeImportService.ImportAsync` (which already returns a parsed `ImportedRecipe`
*before* any DB write); a short in-memory cache means the peek and the subsequent
real import share a single network fetch.

## Why

- The mobile-native entry point: from a recipe page in the phone browser (or another
  app's share sheet), tap Share → Frigorino → it imports. Far smoother than
  copy-pasting a URL.
- A peek before committing matches the existing "save-then-edit, never blind-trust"
  posture and gives the user confidence the right recipe was found before it lands.

## Existing pieces this builds on

- `POST /api/household/{householdId}/recipes/import` (`ImportRecipe.cs`) — the one-shot
  import (fetch + parse + persist + best-effort cover). Unchanged.
- `RecipeImportService.ImportAsync(url, ct) → Result<ImportedRecipe>`
  (`RecipeImportService.cs`) — SSRF-hardened fetch + deterministic JSON-LD parse,
  returns a plain `ImportedRecipe` (`Name`, `Description`, `Servings`, `Ingredients`,
  `SourceName`, `ImageUrl`). Persists nothing — the slice does that.
- Frontend: `CreateRecipeForm.tsx` (the current URL-import entry point on
  `/recipes/create`), `useImportRecipe.ts`, `useUserHouseholds.ts`,
  `useCurrentHousehold.ts`, `useSetCurrentHousehold.ts`.
- PWA manifest is defined inline in `vite.config.ts` (`VitePWA` plugin). The service
  worker is **push-only** (`src/sw.ts`) — no fetch handling — so a GET share-target
  needs nothing from it.
- Auth + active-household gating per route via `requireAuth` (`authGuard.ts`) +
  `RequireHousehold`.

## Architecture

### The peek primitive (shared)

**Backend — preview slice.** New `POST /api/recipes/import/preview`:

- Request `{ url }`, response `{ name, imageUrl }` (image URL is the parsed source URL,
  nullable).
- Auth-only, **household-agnostic** (the peek happens before household selection;
  nothing is persisted, so no household is needed). Wired on a new
  `app.MapGroup("/api/recipes").RequireAuthorization()` group — precedent:
  `/api/me`, `/api/notifications`, `/api/version`.
- Implementation: call `RecipeImportService.ImportAsync(url, ct)`; on success return
  name + `ImageUrl`; on failure map to the **same error codes** the real import uses
  (`invalid_url` → 400 `ValidationProblem` on `Url`; `no_recipe_found` /
  `page_too_large` / `fetch_failed` → 422 with a `code` extension). A blocked private
  IP still reports `fetch_failed` (no SSRF oracle), same as import.

**Backend — fetch cache (kills the double fetch).** The peek and the subsequent real
import both call `ImportAsync` for the same URL within seconds. Add `AddMemoryCache()`
(only `AddDistributedMemoryCache` is registered today, for sessions) and have
`ImportAsync` cache **successful** parses keyed by the trimmed URL with a **2-minute
absolute expiration**:

- Peek warms the cache; the real import hits it → one network fetch+parse, not two.
- Cache successes only — a transient `fetch_failed` must not stick, so retry works.
- `RecipeImportService` is a singleton; a singleton `IMemoryCache` fits with no
  lifetime fuss.
- The cover-image fetch (`FetchImageAsync`) runs only once, at import time — the peek
  shows the source image URL directly in the browser — so there's no double *image*
  fetch.
- `// ponytail: 2-min in-memory cache keyed by URL, success-only; add a size cap if
  volume ever grows.`

**Frontend — shared pieces:**

- `usePreviewRecipeImport()` — mutation hook wrapping the preview endpoint (follows the
  arg-less mutation convention; caller passes `{ body: { url } }`).
- `RecipeImportPreviewCard` — presentational only: image (rendered directly from the
  parsed URL via `<img>`, placeholder when absent or it fails to load) + name; a
  skeleton while the peek is pending; a code-mapped error message on failure. No action
  logic — each consumer renders its own confirm controls.

### Entry point 1 — create page (adopted now)

`CreateRecipeForm` on `/recipes/create`: replace the **blind POST** in the URL-import
path with **peek → confirm**:

1. User enters a URL and triggers the peek.
2. `RecipeImportPreviewCard` shows the parsed name + image.
3. **Confirm** → existing `useImportRecipe` into the **current household** → navigate
   to `/recipes/$recipeId/edit`.

Preserved unchanged: the typed name/description **precedence** (typed values still win
at import) and the separate manual-create path. Only the import trigger changes from
one-shot to peek→confirm. This is the primary, fully IT-able flow (no OS share sheet to
fake).

### Entry point 2 — share receiver

**Manifest** (`vite.config.ts`, in the `VitePWA` `manifest` object):

```js
share_target: {
  action: "/recipes/import",
  method: "GET",
  params: { url: "url", text: "text", title: "title" },
}
```

GET target → push-only SW untouched. Static action path → **no `VITE_` build arg** is
involved, so the Railway build-args rule does not apply here.

**URL extraction helper** — pure function `extractSharedUrl({ url?, text?, title? })`:
returns the first `http(s)` URL found scanning `url` → `text` → `title`. Chrome on
Android usually dumps the page URL into `text` (often noisy, e.g. "Great recipe
https://…"), not `url`, so a plain read of `url` is not enough.

**New route + receiver page** `/recipes/import`:

- `beforeLoad: requireAuth`, wrapped in `RequireHousehold` (same shell as
  `/recipes/create`).
- `validateSearch` runs `extractSharedUrl` over the incoming params → typed
  `sharedUrl?: string`.
- On mount, peek the `sharedUrl` → skeleton → `RecipeImportPreviewCard`. No URL found
  → an empty/error state with a "Create manually" link to `/recipes/create`. Peek
  failure → the code-mapped message + the same escape.
- **Household step** (`useUserHouseholds`): one household → no picker, a single
  **Import** button. More than one → a tappable household list; tapping a row imports
  into that household.
- **Confirm action:** `setCurrentHousehold(chosen)` → `importRecipe({ householdId:
  chosen, body: { url } })` → navigate to `/recipes/$recipeId/edit`. Switching the
  active household first is **required**: the edit page resolves its household from
  `useCurrentHousehold`, so importing into a non-active household and navigating
  without switching would 404. Import is disabled until the peek resolves.

## Data flow

```
share sheet ──(GET /recipes/import?text=…)──> receiver route
  validateSearch → extractSharedUrl → sharedUrl
  peek: POST /api/recipes/import/preview {url}
        → ImportAsync (fetch+parse, cached 2 min) → {name, imageUrl}
  RecipeImportPreviewCard renders name + <img src=imageUrl>
  pick household (skipped if 1)
  confirm: setCurrentHousehold(h)
         → POST /api/household/{h}/recipes/import {url}
           → ImportAsync (cache hit, no 2nd fetch) → persist + cover
         → navigate /recipes/{id}/edit

create page (/recipes/create):
  enter URL → peek (same endpoint, warms cache) → preview card
  confirm → POST /api/household/{current}/recipes/import {url, name?, description?}
          → navigate /recipes/{id}/edit
```

## Error handling

- Preview endpoint maps to the **same** codes as import: `invalid_url` (400 on `Url`),
  `no_recipe_found` / `page_too_large` / `fetch_failed` (422 + `code`). The frontend
  reuses the existing `messageFor(code)` mapping (already in `CreateRecipeForm`); lift
  it to a shared util so both consumers and the preview card use one mapping.
- A failed peek disables import and shows the message + a manual-create escape (a peek
  failure means the real import would fail too).
- `<img>` load failure for the preview image → placeholder; name still shows.
- Cover image and all its failure modes remain best-effort inside the real import
  (unchanged).

## Testing

- **IT (primary, non-PWA):** create page → enter URL → peek shows the stub recipe name
  (asserted via a `data-testid` on the name, dynamic data not translated text) →
  confirm → lands on `/recipes/$recipeId/edit`. Driven by `StubRecipeImportService`
  (returns a name, no `ImageUrl` → image placeholder; keeps the IT network-free).
- **IT:** share receiver — navigate to `/recipes/import?text=<noisy text containing the
  stub URL>` with a single-household test user → peek → confirm → edit. This is the
  runnable check for `extractSharedUrl` (the project has no JS test runner).
- **Unit:** `RecipeImportServiceTests` — two `ImportAsync` calls for the same URL within
  the window perform a single underlying fetch (cache hit), if the existing HTTP stub
  seam allows counting fetches.
- **Optional IT:** multi-household receiver — two-household user → picker shown → tap one
  → import into that household.
- Manifest entry and the SW are static config — not covered by a test.

## Known ceilings (documented, not fixed in this work)

- **iOS Safari has no Web Share Target API** → this is an Android/Chromium-PWA-only
  entry point. The create-page peek works everywhere.
- **Cold launch while logged out drops the share:** `requireAuth` redirects to login
  carrying only `location.pathname` (`authGuard.ts:21`), not the search string, so the
  shared URL is lost. Rare (an installed PWA is normally already authed). Fixing it
  properly means making `authGuard` carry `location.search` for **all** routes — a
  separate cross-cutting change. Flag as its own issue, do not fold in here.
- **Double fetch** is eliminated by the 2-min cache for the common case (peek then
  import promptly). If the user dawdles past the window, the import re-fetches — fine.

## Scope

- **Backend:** preview slice (request/response DTO + endpoint + handler) + new
  `/api/recipes` group registration; `AddMemoryCache()` + cache in `ImportAsync`; one
  unit test.
- **Frontend:** manifest `share_target`; `extractSharedUrl` helper;
  `usePreviewRecipeImport` hook; `RecipeImportPreviewCard` component; shared
  `messageFor` util; create-page peek→confirm refactor; new `/recipes/import` route +
  receiver page; i18n keys (en + de — existing `recipes.import.*` namespace, JSON only).
- **Tests:** two ITs (create-page + receiver), one unit test, optional multi-household
  IT.
- **Docs:** update `knowledge/Recipes.md` (URL-import section) for the peek endpoint,
  cache, and the two entry points.
- **Out of scope:** the `authGuard` search-preservation fix (separate issue); iOS share
  support (platform limit).
