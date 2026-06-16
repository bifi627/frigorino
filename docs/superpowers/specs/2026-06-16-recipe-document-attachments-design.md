# Recipe attachments — documents (PDF)

**Status:** Design approved (brainstorm)
**Date:** 2026-06-16
**Branch:** TBD — `feat/recipe-document-attachments`, off `stage`
**Source idea:** "Document (non-image) attachments" in `IDEAS_Recipes.md`
**Predecessor spec:** `2026-06-15-recipe-attachments-design.md` (the shipped images phase this builds on)

## Summary

The second half of client requirement #5: attach a **PDF** to a recipe as source material (a scanned
recipe card, an exported recipe). This is a **purely additive phase** on top of the shipped image
attachments (PR #126 → stage) — the `RecipeAttachment` table, the "Attachments" edit section with its
add-menu + caption/preview sheets + drag-reorder + soft-delete/undo, the view-page grid + lightbox, the
`recipe-attachment` blob area, and the orphan sweep all already exist. The add-menu already renders a
**disabled "Document" item** as the placeholder for this work.

This phase adds one kind of attachment (document = PDF) alongside images by introducing a `Type`
discriminator on the existing flat table, a document validation + storage path that skips image
processing, and document-specific rendering (icon + filename instead of a thumbnail; open in a new tab
instead of the lightbox). Everything is built **recipe-specific, mirroring the image implementation** —
when lists gain documents later they will mirror this slice the way `RecipeAttachment` mirrored
`ListItem`. We can refactor to a shared abstraction then if it's worth it; we do **not** pre-extract one
now (the list-side shape — attached to a `List` vs a `ListItem` — is still unknown, and the codebase
deliberately runs parallel per-aggregate media stacks).

**Scope: PDF only.** `application/pdf` is the sole accepted document content-type. Office/text formats,
generated first-page thumbnails, in-app PDF rendering, and AI extraction are all out of scope.

## Decisions (from brainstorm)

- **PDF only** — `DocumentContentTypes = ["application/pdf"]`. Matches requirement #5's wording and the
  eventual AI-extraction goal (PDF text is extractable). Other formats are a later additive step.
- **Open in a new tab** — a document tile/row open action fetches the authenticated `/file` blob,
  builds an object URL, and `window.open`s it; the browser's native PDF viewer takes over. No in-app
  viewer, no naked link (a top-level navigation wouldn't carry the `Authorization` header).
- **Icon + filename, no thumbnail** — documents have `ThumbnailStorageKey = null`; rows and tiles show a
  PDF icon + the original filename + caption. No Ghostscript / PDF rasterizer, no new native deps.
- **Single endpoint, content-type routing** — `CreateRecipeAttachment` stays one multipart endpoint and
  one frontend mutation hook; the handler branches on the input content-type (image vs PDF).
- **Mirror images, don't pre-share** — recipe-specific entity/slices/components, parallel to the image
  path. Reuse-across-features is a deliberate later refactor.
- **Testing = unit + manual** — mirror how images shipped (unit tests for the aggregate + content-type
  routing, manual verification of the flow). No new Reqnroll/Playwright IT this phase.

## Data model

`RecipeAttachment` is unchanged except for **one new column** — the existing
`ContentType` / `OriginalFileName` / `FileSizeBytes` / `Caption` / `Rank` columns already describe a PDF.
(The IDEAS sketch speculated "nullable doc-specific columns"; in reality none are needed.)

- New enum `AttachmentType { Image, Document }` (`Frigorino.Domain.Entities`). Stored as `int` (EF
  default — `Image = 0`, `Document = 1`), serialized as its **string name** on the wire per the repo's
  `JsonStringEnumConverter` convention.
- New field on `RecipeAttachment`: `public AttachmentType Type { get; set; }` (defaults to `Image`).
- `ThumbnailStorageKey` (already nullable) is **null for documents**, populated for images. The
  thumbnail invariant becomes per-type: images require a thumbnail, documents must not have one.
- New constant on the entity: `public static readonly string[] DocumentContentTypes = ["application/pdf"];`
  (accepted *input* = stored output for documents — no re-encoding).
- EF: map `Type` (no max-length — it's an int column). No other config change.
- Migration `AddRecipeAttachmentType` — adds the `Type` column with default `0`. Existing rows are
  implicitly `Image`; **no backfill query** needed.

## Domain (aggregate methods on `Recipe`)

Add a sibling to `AddAttachment` rather than overloading it with a mode flag (clean separation —
document and image construction are distinct flows):

- `Result<RecipeAttachment> AddDocumentAttachment(string? caption, StoredFile file)` — validates via a
  new `ValidateAttachmentDocument(file)` (mirrors `ValidateAttachmentImage`): `file.ContentType ==
  "application/pdf"`; `file.StorageKey` non-empty and ≤ `StorageKeyMaxLength`; `file.ThumbnailKey` is
  **null** (documents have no thumbnail); `file.SizeBytes` `> 0` and ≤ `MaxFileSizeBytes` (25 MB);
  caption ≤ `CaptionMaxLength`. Builds the row with `Type = Document`, `ThumbnailStorageKey = null`,
  appended rank.
- `AddAttachment` (image path) is unchanged except it now stamps `Type = Image`.
- `UpdateAttachmentCaption` / `RemoveAttachment` / `RestoreAttachment` /
  `ReplaceRestoredAttachmentRank` / `ReorderAttachment` are **type-agnostic — no change** (caption is the
  only mutable field for both kinds; ordering/soft-delete/restore are identical).

`Property`-tagged generic errors → `ValidationProblem` (400); `EntityNotFoundError` → 404, via the
existing dispatch.

## API slices (`Features/Recipes/Attachments/`)

No new endpoints, no route-group change. The existing single multipart endpoint routes by content-type.

- `CreateRecipeAttachment` — the handler branches **after** the size gate, on `file.ContentType`:
  - In `RecipeAttachment.ImageContentTypes` → existing image path (`IImageProcessor.ProcessAsync` →
    WebP full-res + thumbnail, save both blobs, `recipe.AddAttachment`).
  - In `RecipeAttachment.DocumentContentTypes` → **document path**: skip `IImageProcessor`; save the raw
    PDF bytes as the single full-res blob (`storage.SaveAsync` of `file.OpenReadStream()`); build a
    `StoredFile` with `ThumbnailKey = null`, `ContentType = "application/pdf"`, the original filename,
    and `file.Length`; call `recipe.AddDocumentAttachment(caption, stored)`.
  - Neither list → the existing 400 (`Content type '…' is not an allowed type.`).
  - Both paths reuse the same **upload-before-persist + `RankRetry.SaveWithRetryAsync` +
    compensating-delete** structure already in the handler. The document path has only one blob to
    save/compensate (no thumbnail). Refactor the existing image-specific body so the shared
    save→persist→compensate scaffolding wraps a per-type "produce the blob(s) + `StoredFile`" step.
- `GetRecipeAttachmentFile` (`/file`) — **unchanged**. It already streams the stored blob with the DB
  `ContentType` (so a PDF streams as `application/pdf`) and hard-caches it. The client fetches it as an
  authenticated blob; `Content-Disposition` is irrelevant to the resulting `blob:` object URL, so no
  inline/attachment change is needed.
- `GetRecipeAttachmentThumbnail` (`/thumbnail`) — **unchanged**. It already 404s when
  `ThumbnailStorageKey` is null; documents never call it.
- `RecipeAttachmentResponse` — add `AttachmentType Type` (and `ToProjection`/`From`) so the client can
  branch rendering. Storage keys remain unexposed.

`GetRecipeRevision` — **no change** (already folds `MaxAsync(UpdatedAt)` + `CountAsync()` over all
attachments; documents are just more rows).

`DeleteInactiveItems` — **no change** (already purges soft-deleted attachments + attachments of purged
recipes).

`RecipeAttachmentBlobReferences` (orphan-sweep source) — ensure it **filters out null
`ThumbnailStorageKey`** when enumerating keys (documents contribute only a `StorageKey`). Verify the
existing `Select` doesn't emit nulls; adjust if it does.

Regenerate the TS client (`npm run api`) after the slice + DTO change (adds `Type` to the generated
`RecipeAttachmentResponse`).

## Frontend (`features/recipes/attachments/`)

All recipe-specific, branching on `attachment.type` (`"Image" | "Document"`), mirroring the image
components in place. No shared/extracted components this phase.

### Add-menu — `RecipeAttachmentsSection.tsx`
- **Enable** the currently-disabled "Document" menu item (`recipe-attachment-document`). On click it
  opens the file picker with `accept="application/pdf"` and **no camera** (`capture` cleared).
- Use a second hidden file input for documents (or reuse the existing input, swapping `accept` +
  clearing `capture` imperatively before `.click()`, the way `openPicker` already toggles `capture`).
  Keep one `pendingFile` + the same `RecipeAttachmentPreviewSheet`/`useCreateRecipeAttachment` flow —
  the backend routes by content-type, so the upload call is identical.

### Preview sheet — `RecipeAttachmentPreviewSheet.tsx`
- Branch on the picked `file.type`: for `application/pdf`, render a **doc icon + filename** block
  instead of the `<img>` object-URL preview (no `createObjectURL` for PDFs). Caption field + send button
  unchanged. Title uses `recipes.attachDocumentTitle` for documents.

### Edit row — `RecipeAttachmentRow.tsx`
- For `type === "Document"`: render a fixed **PDF icon** in the thumbnail slot (skip
  `useAttachmentImage` entirely — no blob fetch) and show `attachment.originalFileName` as the primary
  text. Clicking still opens the caption sheet (edit affordance identical). Delete unchanged.

### View tile — `RecipeViewAttachments.tsx`
- For `type === "Document"`: the tile renders a **PDF icon + filename** (no thumbnail fetch). `onOpen`
  fetches the authenticated `/file` blob and opens it in a new tab instead of the lightbox:
  - New hook `useOpenRecipeAttachmentFile(householdId, recipeId)` exposing an imperative
    `open(attachmentId)` that fetches the blob via the generated SDK (`parseAs: "blob"`, which sets the
    blob's `application/pdf` type from `Content-Type`), creates an object URL, `window.open(url,
    "_blank")`, and revokes the URL after a short delay (the new tab has loaded by then). Fetch is
    **click-triggered**, not eager. We deliberately do **not** reuse `useAttachmentImage(..., "file")`
    here — it fetches on mount, which would pre-download every PDF in the grid.
- Image tiles keep the thumbnail + `RecipeAttachmentLightbox` exactly as today.

### i18n (`public/locales/{en,de}/translation.json`)
- Reuse the existing `lists.attachDocument` (the menu label) and `recipes.unsupportedFileType`.
- Add `recipes.attachDocumentTitle` (preview-sheet title) and `recipes.openDocument` (tile aria-label),
  en + de.

## Testing

Mirror how images shipped — **unit + manual**, no new IT.

- **Unit (`Frigorino.Test`)** — `Recipe` aggregate: `AddDocumentAttachment` (accept a valid PDF
  `StoredFile`; reject a non-PDF content-type; reject a **non-null** thumbnail key; reject zero/oversize
  size; reject empty/over-length storage key; caption length). Confirm `AddAttachment` still stamps
  `Type = Image` and that document/image rows share ordering/restore behavior. Pure aggregate logic, no
  DB.
- **Content-type routing** — a unit/slice-level check that a PDF input takes the document path
  (no image processing, null thumbnail) and an image input still takes the image path; an unsupported
  type → 400.
- **Manual verification** — upload a PDF → it appears as an icon+filename row in the edit section and an
  icon tile on the view page → opening the tile opens the PDF in a new tab; caption edit persists;
  reorder mixes documents and images; delete-with-undo works on a document.
- No new Reqnroll/Playwright IT this phase (the deferred image-attachment IT debt remains a separately
  tracked item — out of scope here).

## Verification gate

`npm run tsc` + `npm run lint` + `npm run prettier:check` + `npm run build` + full
`dotnet test Application/Frigorino.sln` (Test + IntegrationTests) + `docker build`. No new native deps,
so the Docker build is drift-checking only.

## Out of scope

- Non-PDF document formats (office/text); multi-file upload.
- Generated PDF first-page thumbnails / in-app PDF rendering.
- A shared cross-feature attachment/document abstraction (deliberate later refactor once lists need it
  and the list-side shape is known).
- AI extraction of recipe content from a document.
- New IT scenarios (image-attachment IT debt included).
