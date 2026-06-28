# Recipes

Recipes are a household-scoped feature alongside Lists and Inventories. A recipe groups ingredient **items** into **sections**, carries external **links** and image/PDF **attachments**, and can be **copied to a shopping list**. Same shape as the rest of the app: one vertical slice per endpoint, `Recipe` is the aggregate root that owns Items/Sections/Links/Attachments, and every mutation goes through an aggregate method returning `FluentResults.Result`.

## Domain (`Frigorino.Domain/Entities/`)

| Entity | Key fields | Notes |
|---|---|---|
| `Recipe.cs` | `Name`, `Description`, `Servings?`, `HouseholdId`, `CreatedByUserId`, `Tags` | Aggregate root. Holds Items/Sections/Links/Attachments collections + all their mutation methods. `CanBeManagedBy(userId, role)` gates writes. Create auto-seeds one unnamed section. `Tags` is a `List<RecipeTag>` **value-set** (curated course + dietary enum, `integer[]` column, no join table), set via `SetTags` (replace-whole-set, dedupes, role-gated, capped at `MaxTags`=10). |
| `RecipeSection.cs` | `Name?`, `Description?`, `Rank` (lexicographic fractional-index per recipe) | Groups items. An unnamed section renders as the default "Ingredients" header. |
| `RecipeItem.cs` | `Text`, `Comment?`, `QuantityValue?`, `QuantityUnit?`, `SectionId`, `Rank` (FI per section) | An ingredient line. Quantity is structured (value + `QuantityUnit`), populated by AI extraction — see below. |
| `RecipeLink.cs` | `Url` (required), `Label?`, `Rank` (FI per recipe) | External source (blog/video). |
| `RecipeAttachment.cs` | `StorageKey`, `ThumbnailStorageKey?`, `ContentType`, `OriginalFileName`, `FileSizeBytes`, `Caption?`, `Type` (`AttachmentType`: `Image`/`Document`), `Rank` (FI per recipe) | Blob lives in storage; row holds metadata + caption. Caption is its own field, never overloaded onto another column. |

Aggregate methods follow the standard add / update / remove (soft-delete) / restore / `ReplaceRestoredXRank` / reorder set per child type. Images go in via `AddAttachment`, PDFs via `AddDocumentAttachment` (distinct factory paths — clean separation of the two media kinds). Max attachment size is enforced on the entity (`RecipeAttachment.MaxFileSizeBytes`).

## API surface (`Frigorino.Features/Recipes/`)

`MapGroup` prefix `/api/household/{householdId:int}/recipes` (+ nested `/{recipeId:int}/items|sections|links|attachments`). All under `RequireAuthorization()`; household membership is checked in each handler.

- **Recipes**: `POST /`, `POST /import` (create from a URL — see URL import below), `GET /`, `GET /{id}`, `GET /{id}/revision` (concurrency token), `PUT /{id}`, `DELETE /{id}`, `POST /{id}/copy-to-list`.
- **Tags**: `PUT /{id}/tags` (replace-whole-set via `Recipe.SetTags`), `POST /{id}/suggest-tags` (synchronous, on-demand AI suggestion — the deliberate exception to "AI runs fire-and-forget"; stateless, persists nothing; see `AI_Classification.md`).
- **Items**: full CRUD + `POST /{itemId}/restore` + `PATCH /{itemId}/reorder`.
- **Sections**: CRUD + restore + reorder. Deleting a section cascades to its items; restore brings them back.
- **Links**: CRUD + restore + reorder.
- **Attachments**: CRUD + restore + reorder, plus `GET /{attachmentId}/file` and `GET /{attachmentId}/thumbnail` (stream the blob, `Cache-Control: private, max-age=31536000, immutable` — 1-year).

## Key flows

### Attachments

Upload (`Attachments/CreateRecipeAttachment.cs`) is a multipart POST (file + optional caption), content-type allow-listed into two paths:

- **Image** (jpeg/png/webp) → `IImageProcessor` (Magick) re-encodes to a full-res WebP + a thumbnail WebP; both are written via `IFileStorage` in the `RecipeAttachment` blob area; the row stores both keys.
- **Document** (`application/pdf`) → raw bytes stored as-is, no processing, no thumbnail (`ThumbnailStorageKey` null).

Blobs are saved **before** the DB row and compensated (best-effort delete) on failure, so a crash mid-insert can't strand a referenced blob; the reverse (orphan blob, no row) is swept later by `ReclaimOrphanBlobs`.

### Quantity extraction (recipe items)

On item create/update the text is triaged by `ItemTextRouter` and, if it needs parsing, `IRecipeQuantityExtractionTrigger.OnItemRouted` queues `ExtractRecipeQuantityJob`, which calls the OpenAI extractor and writes back a clean name + structured `Quantity`. The SPA shows a pending state and polls (`features/recipes/items/useRecipeExtractionPoll.ts`).

### URL import (JSON-LD)

`POST /import` (`ImportRecipe.cs`) takes a `{ url, name?, description? }`, fetches the page server-side, and parses `schema.org/Recipe` JSON-LD into a new recipe the user lands on the **edit** page to review (save-then-edit, never blind-trust). The entry point is the **create recipe page** (`CreateRecipeForm.tsx`) — an inline URL field above the manual name/description form; a name/description the user has already typed **wins** over the parsed values (import-as-prefill, applied server-side). Deterministic only — **no AI** (an AI fallback for unstructured pages is a deferred follow-up in `IDEAS_Recipes.md`).

- **Fetch** (`RecipeImportService.cs`): a singleton SSRF-hardened `HttpClient` whose `SocketsHttpHandler.ConnectCallback` (`RecipeImportConnect.cs`) resolves the host and refuses to connect if **any** resolved IP is non-public (`RecipeImportUrl.IsPublicIpAddress` rejects loopback/private/CGNAT/link-local incl. cloud-metadata `169.254.169.254`/ULA/IPv4-mapped). It runs on every connection including each redirect hop, so DNS-rebinding and redirect-to-private are both covered. Caps: http/https only, ≤5 redirects, 10s timeout, **15 MB** response (streamed, enforced independently of `Content-Length`; over-cap → `page_too_large`), html content-type.
- **Parse** (`JsonLdRecipeParser.cs`): pure, deterministic — regex-extracts `<script type="application/ld+json">` blocks and **deep-walks** every nested object/array (single object / top-level array / `@graph` / `mainEntity`-nested; `@type` as string or array), returning the first Recipe node with a name **and** ingredients (name-less / ingredient-less stubs are skipped). Maps name, description, `recipeYield`→servings, `recipeIngredient` (or legacy `ingredients`), `author`→source label; caps each to its domain max. schema.org ingredients are a **flat array** — there's no standard section concept, so all items land in the one default section.
- **Slice**: membership 404 → import → user-typed `name`/`description` (when present) override the parsed values → `Recipe.Create` → `AddSection` → **save** (so the recipe + section get real ids) → `AddItem` per ingredient (routed through `ItemTextRouter`, so quantity extraction runs) → `AddLink(url, sourceName)` → **save** → fire `IRecipeQuantityExtractionTrigger` per item. Error codes ride back to the SPA: `invalid_url` → 400 ValidationProblem on `Url`; `page_too_large` / `fetch_failed` / `no_recipe_found` → 422 with a `code` extension (a blocked private IP reports as `fetch_failed`, **not** a distinct code — no SSRF oracle).
- **Tests**: integration tests stub the fetch (`StubRecipeImportService`) and cover the create-page flow, typed-name precedence, and frontend URL validation; the real network fetch+parse-success and the size-cap (`page_too_large`) paths are covered by the **unit** tests (`JsonLdRecipeParserTests`, `RecipeImportUrlTests`, `RecipeImportServiceTests`), not the IT.

## Key decisions & rationale

- **Two distinct media paths, not one polymorphic attachment.** Image and document uploads take separate factory paths (`AddAttachment` vs `AddDocumentAttachment`) — images are re-encoded to WebP + thumbnail, documents stored raw with no thumbnail. Keeping the two kinds separate avoids a branchy mode-dependent method (clean domain separation over a "dirty invariant").
- **Caption is its own field**, never overloaded onto a filename or text column.
- **Recipe items deliberately do not chain into product classification.** They never create `Product` catalog rows — that's a list-item concern. Quantity extraction runs; classification does not.
- **Tags are a value-set, not an aggregate child.** A flat `RecipeTag[]` on the recipe (no rank, no soft-delete, no separate table) — they're a small fixed vocabulary, not user-ordered/owned rows. AI suggestions are **suggest-only**: nothing is persisted from the suggest endpoint; the user accepts a chip, which is a normal `SetTags` write. Overview filtering is **client-side** (AND across selected tags, combined with search) — fine at household scale.
- **Blob-before-row, compensate on failure.** Writing the blob before the DB row (and best-effort deleting it on insert failure) means a crash can't leave a row referencing a missing blob; the inverse orphan is reclaimed by the mark-and-sweep, not guarded inline.
- **URL import is JSON-LD-only and always-on.** The deterministic parse needs no vendor/API key, so — unlike the AI features — it has **no `IRecipeImporter` interface, no config gate, no `Null` impl**; the slice calls the concrete `RecipeImportService` directly (the IT seam is a `protected` ctor + `virtual ImportAsync`, not an interface). The `ConnectCallback` IP check is the load-bearing SSRF defense. Bot-protected sites that drop server-side fetches (DataDome etc.) just report `fetch_failed` — not fixable by JSON-LD or AI, only by headless rendering. AI fallback, share-target, cover-image, instructions, and closer schema.org alignment are deferred (`IDEAS_Recipes.md`).

## Cross-feature touchpoints

- **Copy to list → Lists** (`CopyToList/CopyRecipeToList.cs`): `POST /{recipeId}/copy-to-list` takes a target list id + selected items (with optionally scaled quantities) and calls `list.AddItem(text, quantity, comment)` per item — they land unchecked on the list. Because the quantities are already structured it **skips** AI quantity extraction, but it **must** still fire `IProductClassificationTrigger.OnProductReferenced` (fire-and-forget after commit) so the copied names enter the product catalog and promote-to-inventory keeps working. This trigger is easy to forget — it bit this slice in PR #130. See `Lists.md`.
- **Quantity extraction → AI pipeline**: recipe item parsing runs through the shared OpenAI extraction queue and gating. See `AI_Classification.md`.
- **Tag suggestion → AI pipeline**: `POST /{id}/suggest-tags` calls `IRecipeTagSuggester` synchronously in-request (no queue/job). Off by default; the `Null*` impl returns an empty list so the endpoint is always safe to call. See `AI_Classification.md`.
- **Attachments → File storage**: blobs + image processing go through `IFileStorage` / `IImageProcessor` and are reclaimed by `ReclaimOrphanBlobs`. See `File_Storage.md`.

## Frontend (`ClientApp/src/features/recipes/`)

Thin route shells under `src/routes/recipes/` (`index`, `create`, `$recipeId/view`, `$recipeId/edit`) delegate to `features/recipes/pages/`. Sub-areas each follow the one-hook-per-file convention with their own `components/`: `items/`, `sections/`, `links/`, `attachments/` (lightbox, preview sheet, caption sheet, `useAttachmentImage` — note the StrictMode object-URL rule: create+revoke the blob URL in one paired effect, cache the Blob in Query not the URL), `copyToList/`. Root hooks: `useHouseholdRecipes`, `useRecipe`, `useCreateRecipe`, `useUpdateRecipe`, `useDeleteRecipe`, `useRecipeRevision`, `useImportRecipe`.

**Edit page is a "recipe sheet"** (recomposed from a flat stack of five collapsible CRUD panels):

- `components/EditRecipeForm.tsx` — the inline header: borderless title field + servings pill-stepper + multiline description, all saved by one debounced single-PUT (`recipe-description-input` keeps the autosave + blur-flush). `components/RecipeTagSelector.tsx` (now lifted out of the form, into the **Details** accordion below) — grouped selectable course/dietary chips (each toggle is a full `SetTags` write via `useSetRecipeTags`) + a "Suggest tags" button (`useSuggestRecipeTags`) that renders returned tags as ephemeral ghost chips the user taps to accept. Read-only display elsewhere uses `components/RecipeTagChips.tsx` (view page).
- `components/RecipeSourcesStrip.tsx` — the "Sources & photos" strip: link chips + photo/document tiles share a single horizontal row (two `SortableLinkList horizontal` instances — separate reorder endpoints), with the add-link / add-photo controls pinned in the header. The photo tiles live in `data-testid="recipe-section-attachments-content"`. Reuses the attachment preview/caption sheets verbatim.
- **Details accordion** — `RecipeTagSelector` + `RecipeSourcesStrip` render inside one collapsed MUI `Accordion` (`data-testid="recipe-details-accordion"`) on `RecipeEditPage`, between the metadata form and the sections, keeping the optional extras out of the way. Default closed; children stay mounted while collapsed (MUI default), so tag suggestion still fetches behind the fold. ITs that drive the tag/attachment controls open it first (`When I open the recipe details` / the attachments "expand" step clicks the accordion).
- `items/components/RecipeSectionGroup.tsx` — lightweight always-visible section (coral small-caps header + drag handle + ⋮ menu). Name/description fields are collapsed by default; the ⋮ menu toggles **Rename ⇄ Done** (the header already shows the name).
- `items/components/RecipeFooter.tsx` — the composer is always visible and **section-aware**: a target-section switcher chip names where new items land, persisted per-device under `recipe-edit:target-section` (`hooks/usePersistedNumber.ts`).
- Item rows render through the **shared** `SortableList` / `SortableListItem` (drag handle + per-row ⋮ edit/delete menu), opted into the **`dense`** chrome — a bottom hairline divider instead of a per-item card. `items/components/RecipeItemContent.tsx` lays the row out as a flex row: name + italic comment on the left, quantity chip right-aligned on the name's line. `dense` is an opt-in prop (default `false`); **Lists and Inventories now opt in too** — `ListContainer`/`InventoryContainer` pass `dense`, and their content renderers (`lists/items/components/TextItemRenderer.tsx`, `inventories/items/components/InventoryItemContent.tsx`) got the same flex-row treatment (name left, quantity chip right; the List comment / the Inventory expiry-date sit as the sub-caption under the name). The List renderer keeps its tappable quantity/comment + long-press-copy + inline links; the Inventory renderer keeps its colored expiry bar + tappable date.

Removed in the recompose: `RecipeSectionCard`, `RecipeLinksSection`, `RecipeAttachmentsSection`, `RecipeAttachmentRow`, `RecipeLinkRow`, and the then-orphaned shared `CollapsibleSection` + `usePersistedExpanded`.

**Overview is a search hub** (`pages/RecipesPage.tsx`): a **multi-term** search field filters the loaded list live via `searchRecipes.ts` (`rankRecipes` — each term is tiered relevance name > description > ingredient match, and a recipe is kept only if **every** term matches some field (AND); score is the summed per-term tier; empty terms keep newest-first). The field is a `freeSolo` chip `Autocomplete` — picking several about-to-expire ingredients (each pinned as a chip) finds recipes that use all of them, no comma typing. The pending input text is unioned with the committed chips, so a single typed word behaves exactly like the old box. Suggestions are **progressive**: they appear only after 3 typed chars and are drawn from `suggestionPool` (recipes already matching the committed chips + tags), so each chip shrinks what you can add next to ingredients that still lead to a result. Cards (`components/RecipeSummaryCard.tsx`) are one-line with a cover thumbnail (`components/RecipeCoverThumb.tsx`, reusing `useAttachmentImage`); a chevron expands an inline peek (full description + ingredient chips + Open + the ⋮ menu). The list endpoint carries `coverAttachmentId` (first active Image attachment) + `ingredients` for this (`RecipeResponse.ToProjection`). Search is client-side over the full list — fine at household scale, server-side at hundreds of recipes (`TECH_DEBT.md`). `components/RecipeTagFilter.tsx` adds a grouped tag-chip row above the search field; selected tags narrow the list (AND across tags) before search ranking, also client-side. The row is **facet-aware**: it only shows tags present in the visible subset (plus any already selected, so they stay toggleable), so narrowing by ingredient or tag shrinks the available tag chips to what's still reachable. Tag vocabulary + label hook live in `features/recipes/tags.ts`. An **Import from URL** action (Download icon) in the head bar opens `components/ImportRecipeSheet.tsx` — paste a recipe URL → `POST /import` (`useImportRecipe`) → success toast → navigate to the new recipe's edit page to review; failures render a code-mapped inline error. (Moving this entry point onto the create page + user-input precedence is a deferred follow-up — see `IDEAS_Recipes.md`.)

## Links out

- Slice pattern: `Vertical_Slices.md`
- API client + hook conventions: `API_Integration.md`
- Styling: `Frontend_Styling.md`
- AI quantity extraction (pipeline + gating): `AI_Classification.md`
- Blob storage + image processing + reclamation: `File_Storage.md`
- Consumes list items via: `Lists.md`
- Rollout history / parked ideas: `../IDEAS_Recipes.md`
