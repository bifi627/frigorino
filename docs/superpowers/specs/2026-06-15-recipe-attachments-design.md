# Recipe attachments (images)

**Status:** Design approved (brainstorm) — prerequisite **landed** (blob-area refactor on branch `refactor/blob-storage-areas`)
**Date:** 2026-06-15
**Branch:** TBD — `feat/recipe-attachments`, off `stage` once the blob-area refactor and `feat/recipe-source-links` have merged
**Source idea:** "File / image / document attachments" in `IDEAS_Recipes.md`
**Predecessor specs:** `2026-06-15-recipe-source-links-design.md` (sibling pattern), `2026-06-03-rich-list-items-2-image-items-design.md` + `2026-06-03-rich-list-items-4-prod-storage-design.md` (blob infra), `2026-06-04-replace-imagesharp-with-magicknet-design.md` (image processor)

## Summary

Part of client requirement #5: attach **images** to a recipe as source material — a photo of the
finished dish, or a phone snap of a handwritten/printed recipe card. This is a **separate sibling
feature** to the already-shipped source links — `RecipeLink` is untouched; we add an images-only
`RecipeAttachment` table, its own slices, and a **new "Attachments" section** on the edit page plus a
thumbnail-grid block on the view page. (Links and files are genuinely different concepts — a URL
reference vs an uploaded blob — so they stay separate rather than being unified into one table.)

**Scope is images only.** PDF / document attachments are a deliberate *later additive phase* (the
"why" of requirement #5 still includes PDFs; this spec just ships the image slice first). Because
there's only one kind of attachment right now, there is **no type discriminator** — every attachment
is an image with a generated thumbnail. Adding documents later introduces the `Type` column +
relaxes the thumbnail invariant, all additive.

The feature reuses the shipped blob infrastructure almost wholesale: `IFileStorage`
(Save/Open/Delete), the GCS/Local backends, `MagickImageProcessor` (WebP full-res + thumbnail), the
`StoredFile` VO, the upload-before-persist + compensating-delete pattern, the orphan-blob sweep, and
the frontend blob-caching pattern (cache the `Blob`, per-consumer object URL). Affordances mirror the
links feature: optional caption, fractional drag-reorder, soft-delete + undo, and revision-token
participation.

## Prerequisite (landed)

The **blob-area refactor** this feature depended on has **landed** (branch `refactor/blob-storage-areas`).
It replaced the single blob prefix + `ListItems`-hardcoded orphan sweep with per-feature/per-env
`BlobArea`s: `BlobAreas` constants, keyed-DI `IFileStorage`/`IFileStorageMaintenance` per area
(prefix `{env}/{area}`, composed from `FileStorage:Environment`), and an `IBlobReferenceSource`
registry the sweep iterates per folder (`ReclaimOrphanBlobs` resolves each area's keyed storage by
name). Adding a blob feature now needs only a new area constant + an `IBlobReferenceSource` — the
sweep itself does not change.

**What this feature then contributes on top of the refactor:**
- A `recipe-attachment` `BlobArea` (prefix `{env}/recipe-attachment`), with its keyed `IFileStorage`
  + `IFileStorageMaintenance` registered.
- A `RecipeAttachmentBlobReferences : IBlobReferenceSource` (`AreaName = "recipe-attachment"`)
  returning every `StorageKey` + `ThumbnailStorageKey` across **all** `RecipeAttachment` rows (active
  **and** soft-deleted — soft-deleted rows keep their blob for undo until purged). Registered in DI;
  the refactored sweep picks it up automatically.

## Scope

In scope:
1. `RecipeAttachment` entity (image only: storage keys, content-type/filename/size metadata, optional
   `Caption`, fractional `Rank`, soft-delete). **No type discriminator.**
2. Migration `AddRecipeAttachments` (no backfill — existing recipes simply have zero attachments).
3. `Recipe` aggregate methods: add / update-caption / remove / restore / replace-restored-rank /
   reorder. Image invariants (content-type, thumbnail-present, size, key) live in the aggregate.
4. Upload + caption + reorder + delete + restore + file-serve + thumbnail-serve slices under a new
   `recipeAttachments` route group.
5. `GetRecipeRevision` extended to fold attachments into the revision token (collaborative refresh).
6. `DeleteInactiveItems` extended to purge soft-deleted attachments + attachments of purged recipes.
7. The `recipe-attachment` blob area + `RecipeAttachmentBlobReferences` source (sits on the refactor).
8. Editor: a new collapsible "Attachments" section (after "Source links"): an upload button + a
   drag-reorderable vertical list of rows (thumbnail + caption field + delete).
9. View: an "Attachments" thumbnail-grid block (after the "Sources" block); tiles open the full-res
   lightbox; caption shown small under each tile; hidden when there are none.

Out of scope:
- **Document / PDF attachments** — the explicit *next* phase. It adds a `Type`
  (`Image`/`Document`) discriminator column (+ migration, backfill existing rows to `Image`), relaxes
  the thumbnail invariant (documents have no thumbnail → a doc tile), and accepts `application/pdf`
  without image processing. Designed to be purely additive on top of this spec.
- AI extraction of ingredients/instructions from an attachment's content.
- Editing image bytes in place (replace = delete + re-add; only the caption is mutable).
- Non-image types, client-side cropping, multi-file drag-and-drop upload.
- Per-attachment access control beyond the existing household-membership check.

---

## Data model

New flat entity `RecipeAttachment` (sibling of `RecipeLink`). One kind of row today (image), so no
discriminator column:

| Field                 | Type      | Notes                                                                  |
| --------------------- | --------- | ---------------------------------------------------------------------- |
| `Id`                  | int PK    |                                                                        |
| `RecipeId`            | int FK    | → `Recipe` (cascade delete)                                            |
| `StorageKey`          | string    | full-res WebP blob key (required); max **200**                         |
| `ThumbnailStorageKey` | string?   | thumbnail blob key; nullable column but **always populated** for images |
| `ContentType`         | string    | always `image/webp` (stored output); max **255**                       |
| `OriginalFileName`    | string?   | as uploaded; max **255**                                               |
| `FileSizeBytes`       | long      | stored (full-res) size; `> 0`, `≤ 25 MB`                               |
| `Caption`             | string?   | optional; max **255**; trimmed, empty → null                          |
| `Rank`                | string    | fractional index for ordering (`C` collation)                         |
| `IsActive`            | bool      | soft-delete (default true)                                            |
| `CreatedAt`           | DateTime  | auto-stamped in `SaveChangesAsync`                                    |
| `UpdatedAt`           | DateTime  | auto-stamped in `SaveChangesAsync`                                    |

`ThumbnailStorageKey` is left nullable (matches `ListItem`, and avoids a column change when documents
land) but the aggregate **requires** it on add — every image has a thumbnail.

Constants on `RecipeAttachment` (its own source of truth, mirroring `ListItem`'s values — no
cross-aggregate coupling): `MaxFileSizeBytes = 25 * 1024 * 1024`, `StorageKeyMaxLength = 200`,
`ContentTypeMaxLength = 255`, `OriginalFileNameMaxLength = 255`, `CaptionMaxLength = 255`,
`ImageContentTypes = ["image/jpeg","image/png","image/webp"]` (accepted *inputs*; stored output is
always `image/webp`).

- `Recipe` gains `public ICollection<RecipeAttachment> Attachments { get; set; } = new List<…>();`.
- EF config (mirror `RecipeLinkConfiguration`): FK cascade, `HasMaxLength` on the string columns,
  `Rank` text + `C` collation, and a **partial unique index** on `(RecipeId, Rank)` filtered
  `WHERE "IsActive"` — `UX_RecipeAttachments_RecipeId_Rank_Active` — plus supporting indexes on
  `RecipeId` / `(RecipeId, IsActive)`.
- `DbSet<RecipeAttachment> RecipeAttachments` on `ApplicationDbContext`.
- Migration `AddRecipeAttachments`. **No backfill** — zero attachments is a valid state.

## Domain (aggregate methods on `Recipe`)

Mirror the link methods (collaborative — any household member, no role gate; each bumps
`Recipe.UpdatedAt` for the revision token). The add method folds in the image validation from
`List.AddMediaItem`.

- `Result<RecipeAttachment> AddAttachment(string? caption, StoredFile file)` — validates, appends
  rank. Rules:
  - `file.ContentType` == `image/webp` (the processor's stored output; an input allowlist is enforced
    upstream in the slice before processing — see below).
  - `file.StorageKey` non-empty and ≤ `StorageKeyMaxLength`.
  - `file.ThumbnailKey` **present** (every image has a thumbnail).
  - `file.SizeBytes` `> 0` and ≤ `MaxFileSizeBytes`.
  - `caption` ≤ `CaptionMaxLength` (trim; empty → null).
- `Result<RecipeAttachment> UpdateAttachmentCaption(int attachmentId, string? caption)` — **caption is
  the only mutable field** (image bytes are immutable; replacing the image is delete + re-add).
- `Result RemoveAttachment(int attachmentId)` — soft-delete (`IsActive=false`); undo-able.
- `Result<RecipeAttachment> RestoreAttachment(int attachmentId)` — reactivate; de-collide rank if a
  now-active sibling took it (mirror the link restore guard).
- `Result<RecipeAttachment> ReplaceRestoredAttachmentRank(int attachmentId)` — re-mint rank on 23505
  (used by the restore slice's `RankRetry`).
- `Result<RecipeAttachment> ReorderAttachment(int attachmentId, int afterAttachmentId)` — fractional
  reindex; `afterId == 0` means move to top (same convention as items/sections/links).

`Property`-tagged generic errors map to `ValidationProblem` (400) via the existing `ResultExtensions`
dispatch; `EntityNotFoundError` → 404.

## API slices (`Features/Recipes/Attachments/`)

Route group in `Program.cs` (mirror the `recipeLinks` registration):
`var recipeAttachments = app.MapGroup("/api/households/{householdId:int}/recipes/{recipeId:int}/attachments").RequireAuthorization().WithTags("RecipeAttachments");`
with `recipeAttachments.MapGetRecipeAttachments()` … etc.

- `GetRecipeAttachments` — `GET …/attachments` → `RecipeAttachmentResponse[]`, active, ordered by
  `Rank` (inline EF projection, handler-only read).
- `CreateRecipeAttachment` — `POST …/attachments` (**multipart**: `file`, `caption?`) → 201
  `RecipeAttachmentResponse`. Flow mirrors `Lists/Items/CreateMediaItem`:
  1. App-level size guard (`≤ MaxFileSizeBytes`) + input content-type allowlist check
     (`ImageContentTypes`) before processing → 400 on violation.
  2. `IImageProcessor.ProcessAsync` → WebP full-res + thumbnail (stored content-type `image/webp`).
  3. **Upload-before-persist**: `storage.SaveAsync` the full-res + thumbnail blobs, build a
     `StoredFile`, call `recipe.AddAttachment(...)` inside `RankRetry.SaveWithRetryAsync`. On any DB
     failure, **compensate** by `DeleteAsync`-ing the saved blob(s).
  4. Storage injected per area: `[FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage storage`.
- `UpdateRecipeAttachment` — `PUT …/attachments/{attachmentId}` (body: `{ caption }`).
- `DeleteRecipeAttachment` — `DELETE …/attachments/{attachmentId}` → 204 (soft-delete).
- `RestoreRecipeAttachment` — `POST …/attachments/{attachmentId}/restore` → `RecipeAttachmentResponse`
  (undo path; `RankRetry` + `ReplaceRestoredAttachmentRank` on 23505).
- `ReorderRecipeAttachment` — `PUT …/attachments/{attachmentId}/reorder` (body: `{ afterId }`).
- `GetRecipeAttachmentFile` — `GET …/attachments/{attachmentId}/file` → `FileStreamHttpResult`
  streamed from the area storage with the DB `ContentType` + sanitized `OriginalFileName`; hard cache
  `Cache-Control: private, max-age=31536000, immutable` (GUID keys are immutable). 404 if absent.
- `GetRecipeAttachmentThumbnail` — `GET …/attachments/{attachmentId}/thumbnail` → thumbnail stream
  (every image has one; keep a defensive 404 if `ThumbnailStorageKey` is somehow null).
- `RecipeAttachmentResponse` — `{ id, recipeId, contentType, originalFileName, fileSizeBytes,
  caption, rank }`. **Storage keys are never exposed** — the client fetches `/file` and `/thumbnail`.

`GetRecipeRevision` extended: fold attachment `MaxAsync(UpdatedAt)` + `CountAsync()` into the existing
revision computation so a collaborator's attachment change advances the token (same mechanism as
links/sections/items).

`DeleteInactiveItems` extended to purge soft-deleted `RecipeAttachments` and attachments of recipes
being purged. (Their blobs then become reclaimable by the per-area orphan sweep on a later run.)

Regenerate the TS client (`npm run api`) after the slices land.

## Frontend

### Hooks — `features/recipes/attachments/` (one-hook-per-file, spread generated options)

- `useRecipeAttachments(householdId, recipeId, enabled)` — query; `enabled` guards both ids `> 0`,
  `staleTime` ~30s. Mirrors `useRecipeLinks`.
- `useCreateRecipeAttachment` — arg-less **multipart** mutation; caller passes
  `{ path, body: { file, caption } }` (FormData; no manual `Content-Type`). `onSuccess` invalidates
  `getRecipeAttachmentsQueryKey`. Mirrors `useCreateMediaItem`. Reconcile the `Date.now()` temp id →
  real server id in `onSuccess` (per the optimistic-create rule) so edit-after-add hits the real id.
- `useUpdateRecipeAttachment` — caption update (debounced in the row); `onSuccess` invalidates.
- `useDeleteRecipeAttachment` — optimistic remove + undo toast (`t("recipes.attachmentDeleted")`) →
  `restore.mutate`; `onSettled` debounced-invalidate. Mirrors `useDeleteRecipeLink`.
- `useRestoreRecipeAttachment` — `onSuccess` invalidates.
- `useReorderRecipeAttachment` — optimistic reposition (`afterId === 0` = top). Mirrors
  `useReorderRecipeLink`.
- `useAttachmentImage(householdId, recipeId, attachmentId, variant, enabled)` — `variant`
  `"thumbnail" | "file"`; fetches the blob (`parseAs: "blob"`), `staleTime: Infinity`, and yields a
  **per-consumer object URL** created+revoked in **one paired `useEffect`** (StrictMode-safe; cache
  the `Blob`, not the URL). Direct mirror of `useItemImage`.
- `useRecipeRevision` — add a `useRevisionInvalidation` for the attachments key with the same
  `isLocalMutation` predicate (mirror the links addition).

### Edit page — new "Attachments" section

A new collapsible `CollapsibleSection` titled `t("recipes.attachments")`, placed **after** the
"Source links" section (before the ingredient-section accordion) in `RecipeEditPage.tsx`. Persisted
via `usePersistedExpanded("recipe-edit-section:attachments", false)` — **defaults collapsed**. New
component `RecipeAttachmentsSection.tsx`:

- **Upload affordance:** an "Add attachment" button that opens a file picker
  (`accept="image/jpeg,image/png,image/webp"`; offer camera capture on mobile, reusing the picker
  logic from `components/composer/features/attachComposerFeature.tsx`). On pick, call
  `useCreateRecipeAttachment`. Show a busy state while uploading; surface server errors
  (size/type) inline (`t("recipes.uploadFailed")` / field-specific messages).
- **Sortable list:** reuse the generic `SortableLinkList` (dnd-kit) of `RecipeAttachmentRow`s.
- `RecipeAttachmentRow.tsx`: drag handle (`recipe-attachment-drag-handle-{id}`), a small **thumbnail**
  (`useAttachmentImage(..., "thumbnail")`), a caption `TextField` (debounced save via
  `useUpdateRecipeAttachment`, same `latest`-ref-in-`useLayoutEffect` + timer-cleanup pattern as
  `RecipeLinkRow`), and a delete `IconButton`. Testids: `recipe-attachment-row-{id}`,
  `recipe-attachment-{id}-caption-input`, `recipe-attachment-{id}-delete`.

The block renders even with zero attachments (it's the affordance to add the first one).

### View page — "Attachments" thumbnail grid

`RecipeViewAttachments.tsx`, rendered in `RecipeViewPage.tsx` **after** `RecipeViewLinks` and before
the ingredient list. Reads `useRecipeAttachments`; **hidden when there are none**. Layout: an
overline `t("recipes.attachments")` heading + a responsive grid (2–3 per row) of square thumbnail
tiles (`useAttachmentImage(..., "thumbnail")`); tapping a tile opens the existing `ImageLightbox`
with the full-res image (`variant="file"`). Caption shown small under the tile (omitted when empty).
Testids: `recipe-view-attachments`, `recipe-attachment-{id}`.

### Reuse inventory (no new infra)

`IFileStorage`/`IFileStorageMaintenance` (per-area, post-refactor), `MagickImageProcessor` /
`IImageProcessor` / `ProcessedImage`, `StoredFile`, `RankRetry`, `FractionalIndex`, `SortableLinkList`,
`ImageLightbox`, the `useItemImage` blob-caching pattern, the `attachComposerFeature` picker logic.

## Cross-cutting

### i18n (`public/locales/{en,de}/translation.json`, under `recipes`)

`attachments`, `addAttachment`, `attachmentCaption`, `attachmentCaptionPlaceholder`,
`deleteAttachment`, `attachmentDeleted`, `uploadFailed`, `fileTooLarge`, `unsupportedFileType`.
(en + de.)

### Dockerfile

No new project. Magick.NET native deps are already present (list-item media uses them) — no change.
Still `docker build` at the end per the verification gate.

### Testing

- **Unit (`Frigorino.Test`)** — `Recipe` aggregate: `AddAttachment` (valid image with thumbnail;
  reject missing thumbnail; reject wrong stored content-type; reject zero/oversize size; reject
  empty/over-length storage key; caption length), `UpdateAttachmentCaption`,
  `RemoveAttachment`/`RestoreAttachment` (incl. rank de-collision), `ReorderAttachment` ordering.
  Pure aggregate logic, no DB.
- **Integration (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Testcontainers)** — upload an
  image → it appears as a thumbnail on the view, lightbox opens; caption edit persists; reorder;
  delete-with-undo; revision token advances on an attachment change; `DeleteInactiveItems` purge seeds
  an active + an inactive attachment and asserts only the active survives; the per-area orphan sweep
  keeps a referenced `recipe-attachment` blob and reclaims an unreferenced aged one in that folder
  (and does **not** touch the other area). Plus a DB test that `RecipeAttachmentBlobReferences`
  returns keys for active **and** soft-deleted rows. Steps assert on **testids / data-attributes
  only**, never translated text; step bindings reused across keywords are **double-decorated**
  `[Given]`+`[When]` (this repo's Reqnroll is keyword-sensitive).
- No frontend JS test runner exists; UI behavior is covered by the Playwright IT. Run `npm run build`
  before the IT (the harness serves `ClientApp/build`).

### Verification gate

`npm run tsc` + `npm run lint` + `npm run prettier:check` + `npm run build` + full
`dotnet test Application/Frigorino.sln` (Test + IntegrationTests) + `docker build`.

## Decisions / defaults

- **Separate sibling feature**, not unified with source links — distinct concepts (URL ref vs blob);
  `RecipeLink` is untouched; two sections on the edit/view pages.
- **Images only** (re-encoded to WebP with a generated thumbnail). PDF/documents are the explicit
  next additive phase.
- **No type discriminator** now — one kind of attachment. Documents later add a `Type` column +
  relax the thumbnail invariant (additive migration).
- **Caption + drag-reorder + soft-delete/undo** — full parity with the links feature.
- **View = thumbnail grid** (lightbox); **edit = vertical sortable list** (room for drag handles +
  caption fields).
- **Caption is the only mutable field** — image bytes are immutable; replacing an image is delete +
  re-add.
- **Storage keys never leave the server** — the client uses `…/file` and `…/thumbnail` (hard-cached,
  immutable GUID keys).
- **Blob-area refactor has landed** (branch `refactor/blob-storage-areas`); this feature contributes a
  `recipe-attachment` area + an `IBlobReferenceSource`.
- Edit-page "Attachments" section **defaults collapsed**; **no backfill** (zero attachments is valid).
- **No data migration of existing list-item blobs** — barely used; data loss accepted (2026-06-15).
