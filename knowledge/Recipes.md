# Recipes

Recipes are a household-scoped feature alongside Lists and Inventories. A recipe groups ingredient **items** into **sections**, carries external **links** and image/PDF **attachments**, and can be **copied to a shopping list**. Same shape as the rest of the app: one vertical slice per endpoint, `Recipe` is the aggregate root that owns Items/Sections/Links/Attachments, and every mutation goes through an aggregate method returning `FluentResults.Result`.

For rollout history / parked ideas see `../IDEAS_Recipes.md`.

## Domain (`Frigorino.Domain/Entities/`)

| Entity | Key fields | Notes |
|---|---|---|
| `Recipe.cs` | `Name`, `Description`, `Servings?`, `HouseholdId`, `CreatedByUserId` | Aggregate root. Holds Items/Sections/Links/Attachments collections + all their mutation methods. `CanBeManagedBy(userId, role)` gates writes. Create auto-seeds one unnamed section. |
| `RecipeSection.cs` | `Name?`, `Description?`, `Rank` (lexicographic fractional-index per recipe) | Groups items. An unnamed section renders as the default "Ingredients" header. |
| `RecipeItem.cs` | `Text`, `Comment?`, `QuantityValue?`, `QuantityUnit?`, `SectionId`, `Rank` (FI per section) | An ingredient line. Quantity is structured (value + `QuantityUnit`), populated by AI extraction — see below. |
| `RecipeLink.cs` | `Url` (required), `Label?`, `Rank` (FI per recipe) | External source (blog/video). |
| `RecipeAttachment.cs` | `StorageKey`, `ThumbnailStorageKey?`, `ContentType`, `OriginalFileName`, `FileSizeBytes`, `Caption?`, `Type` (`AttachmentType`: `Image`/`Document`), `Rank` (FI per recipe) | Blob lives in storage; row holds metadata + caption. Caption is its own field, never overloaded onto another column. |

Aggregate methods follow the standard add / update / remove (soft-delete) / restore / `ReplaceRestoredXRank` / reorder set per child type. Images go in via `AddAttachment`, PDFs via `AddDocumentAttachment` (distinct factory paths — clean separation of the two media kinds). Max attachment size is enforced on the entity (`RecipeAttachment.MaxFileSizeBytes`).

## API surface (`Frigorino.Features/Recipes/`)

`MapGroup` prefix `/api/household/{householdId:int}/recipes` (+ nested `/{recipeId:int}/items|sections|links|attachments`). All under `RequireAuthorization()`; household membership is checked in each handler.

- **Recipes**: `POST /`, `GET /`, `GET /{id}`, `GET /{id}/revision` (concurrency token), `PUT /{id}`, `DELETE /{id}`, `POST /{id}/copy-to-list`.
- **Items**: full CRUD + `POST /{itemId}/restore` + `POST /{itemId}/reorder`.
- **Sections**: CRUD + restore + reorder. Deleting a section cascades to its items; restore brings them back.
- **Links**: CRUD + restore + reorder.
- **Attachments**: CRUD + restore + reorder, plus `GET /{attachmentId}/file` and `GET /{attachmentId}/thumbnail` (stream the blob, `Cache-Control: immutable`, 1-year).

## Attachments flow

Upload (`Attachments/CreateRecipeAttachment.cs`) is a multipart POST (file + optional caption), content-type allow-listed into two paths:

- **Image** (jpeg/png/webp) → `IImageProcessor` (Magick) re-encodes to a full-res WebP + a thumbnail WebP; both are written via `IFileStorage` in the `RecipeAttachment` blob area; the row stores both keys.
- **Document** (`application/pdf`) → raw bytes stored as-is, no processing, no thumbnail (`ThumbnailStorageKey` null).

Blobs are saved **before** the DB row and compensated (best-effort delete) on failure, so a crash mid-insert can't strand a referenced blob; the reverse (orphan blob, no row) is swept later by `ReclaimOrphanBlobs`. See `File_Storage.md` for the storage + image-processing + reclamation details.

## Quantity extraction (recipe items)

On item create/update the text is triaged by `ItemTextRouter` and, if it needs parsing, `IRecipeQuantityExtractionTrigger.OnItemRouted` queues `ExtractRecipeQuantityJob`, which calls the OpenAI extractor and writes back a clean name + structured `Quantity`. Recipe items deliberately **do not** chain into product classification (they never create `Product` catalog rows — that's a list-item concern). The SPA shows a pending state and polls (`features/recipes/items/useRecipeExtractionPoll.ts`). Full pipeline + gating in `AI_Classification.md`.

## Copy to list (`CopyToList/CopyRecipeToList.cs`)

`POST /{recipeId}/copy-to-list` takes a target list id + selected items (with optionally scaled quantities) and calls `list.AddItem(text, quantity, comment)` per item — they land unchecked on the list. Because the quantities are already structured it **skips** AI quantity extraction, but it **must** still fire `IProductClassificationTrigger.OnProductReferenced` (fire-and-forget after commit) so the copied names enter the product catalog and promote-to-inventory keeps working. This trigger is easy to forget — it bit this slice in PR #130 (see memory note on the list-item classification contract).

## Frontend (`ClientApp/src/features/recipes/`)

Thin route shells under `src/routes/recipes/` (`index`, `create`, `$recipeId/view`, `$recipeId/edit`) delegate to `features/recipes/pages/`. Sub-areas each follow the one-hook-per-file convention with their own `components/`: `items/`, `sections/`, `links/`, `attachments/` (lightbox, preview sheet, caption sheet, `useAttachmentImage` — note the StrictMode object-URL rule from memory), `copyToList/`. Root hooks: `useHouseholdRecipes`, `useRecipe`, `useCreateRecipe`, `useUpdateRecipe`, `useDeleteRecipe`, `useRecipeRevision`.
