# List document attachments (rich-list-items sub-feature #3) — design

- **Date:** 2026-06-21
- **Status:** Approved (design)
- **Branch:** `feat/list-document-items` (off `stage`) — proposed
- **Parent:** [`2026-05-23-rich-list-items-design.md`](2026-05-23-rich-list-items-design.md) (sub-feature #3 in its decomposition table)

## Summary

List items can be `Text` or `Image` today. This sub-feature ships the last rich-list-items piece:
a **`Document`** item that stores a PDF (warranty / manual / receipt) and still checks off,
reorders, soft-deletes and restores like any other item. It opens in a new browser tab on tap
(native PDF viewer); there is no in-app PDF preview.

The work is mostly **composition** — the document path already exists end-to-end for *recipe*
attachments and the *domain* layer for list items already validates `Document`. The remaining gaps
are one backend slice branch and a handful of small frontend additions, all direct mirrors of code
already on `stage`.

## What already exists on `stage` (do not rebuild)

Verified 2026-06-21:

- **`ListItemType.Document = 2`** — the enum value is present.
- **`List.AddMediaItem(type, caption, file)`** (`Frigorino.Domain/Entities/List.cs`) **already fully
  validates `Document`**: routes the content-type check to `ListItem.DocumentContentTypes`
  (`["application/pdf"]`), enforces the "documents must NOT carry a thumbnail key" invariant, and
  applies the shared size cap / filename / storage-key checks. No domain change is needed.
- **`ListItem`** already carries every column required — `StorageKey`, nullable
  `ThumbnailStorageKey`, `OriginalFileName`, `ContentType`, `FileSizeBytes` — plus the limit
  constants (`MaxFileSizeBytes` = 25 MB, `OriginalFileNameMaxLength`, `ContentTypeMaxLength`,
  `StorageKeyMaxLength`). **No new column → no migration.**
- **`ListItemResponse`** already projects `Type`, `FileName`, `ContentType`, `FileSize` (both the
  `From` factory and the EF `ToProjection`).
- **`GetItemFile`** (`GET .../items/{id}/file`) is content-type-agnostic — it already streams any
  blob with its stored `ContentType` and a sanitized download name; it serves a PDF unchanged.
- **`UpdateItem`** already permits caption-only edits on any non-`Text` media item and rejects
  text/quantity mutations on them.
- **Reorder / toggle / soft-delete / restore / orphan-blob reclaim** are all type-agnostic and apply
  to a `Document` item with no change.

The parent decomposition predicted #3 would "reuse #2's `CreateMediaItem` wholesale — no new
pipeline." That is *almost* true; the one exception is documented next.

## The single backend gap

`CreateMediaItem.cs` (the list upload slice) **unconditionally** runs the ImageSharp pipeline
(`imageProcessor.ProcessAsync`) and saves *two* blobs (full-res + thumbnail). For a PDF this both
fails (ImageSharp can't decode a PDF) and would violate the no-thumbnail invariant.

The fix already exists verbatim in the **recipe** slice `CreateRecipeAttachment.cs` (lines ~66-108),
which branches `isImage` / `isDocument`. Port that branch into `CreateMediaItem`:

1. **Input content-type pre-filter** — accept `ListItem.ImageContentTypes` ∪
   `ListItem.DocumentContentTypes`; anything else → 400 `ValidationProblem` on `file`. (The list
   slice currently has no pre-filter — it leans on the image processor to reject.)
2. **Image path** — unchanged: `ProcessAsync` → save full-res + thumbnail → `StoredFile(key,
   thumbKey, …)` → `AddMediaItem(Image, …)`.
3. **Document path** — store the raw upload stream (`storage.SaveAsync(upload, ct)`), build
   `StoredFile(storageKey, thumbnailKey: null, file.ContentType, file.FileName, file.Length)`, call
   `AddMediaItem(Document, caption, stored)`.

The existing orphan-safe save→compensate ordering and the `RankRetry.SaveWithRetryAsync` wrapper
stay exactly as-is for both branches.

## Key decisions & rationale

1. **PDF only in v1** (`application/pdf`). Matches the parent design and the recipe precedent; zero
   domain change. Widening later is a one-line allowlist edit on `ListItem` + the slice pre-filter.
2. **Tap → open in a new tab** (native browser PDF viewer), not force-download and not an embedded
   in-app preview. Mirrors `useOpenRecipeAttachmentFile`: the `/file` endpoint requires the Firebase
   bearer token, so a naked `<a href>`/`window.open(url)` would 401 — instead fetch the authed blob,
   create an object URL, and point a synchronously-opened tab at it. Consistent with recipes,
   satisfies "no inline preview in our UI", best mobile UX.
3. **Frontend derives `Image` vs `Document` from the picked file's MIME** in `handleSendMedia`
   (`file.type === "application/pdf" ? "Document" : "Image"`). The `AttachPayload` stays `{ file }`;
   no new plumbing. The server remains the gatekeeper (the `type` form value is validated against the
   content-type allowlist), so a spoofed MIME just yields a clean 400.
4. **Reuse `MediaPreviewSheet`** with a small branch (doc icon + filename instead of the `<img>`
   preview) rather than a separate sheet. The file + caption + send flow is identical.
5. **Reuse one hidden file input** in the attach composer, toggling `accept` imperatively before
   `.click()` — the same technique already used for the camera `capture` attribute.

## Backend changes

Only `Frigorino.Features/Lists/Items/CreateMediaItem.cs` changes (the port described in "The single
backend gap"). No new slice, no DTO change, no domain change, **no migration**.

## Frontend changes (all small, mirroring recipes / the image path)

1. **`components/composer/features/attachComposerFeature.tsx`** — remove `disabled` from the
   "Document" `MenuItem`; on click, set the hidden input's `accept` to `application/pdf` (clear it
   back to the image list for the photo options) before `input.click()`.
2. **`features/lists/pages/ListViewPage.tsx`** — in `handleSendMedia`, set
   `type: pendingFile.type === "application/pdf" ? "Document" : "Image"` instead of the hard-coded
   `"Image"`.
3. **`features/lists/items/components/MediaPreviewSheet.tsx`** — when the picked file is a PDF, render
   a document icon + filename in place of the image preview; generalize the dialog title.
4. **New `features/lists/items/useOpenItemFile.ts`** — mirror `useOpenRecipeAttachmentFile`, pointed
   at `/api/household/{householdId}/lists/{listId}/items/{itemId}/file`; synchronous `window.open("",
   "_blank")` → fetch authed blob → `win.location.href = objectUrl` → revoke after a delay.
5. **New `features/lists/items/components/DocumentItemRenderer.tsx`** — mirror `ImageItemRenderer` /
   `RecipeAttachmentRow`: a doc icon + `item.fileName` (+ optional `item.comment` caption), the whole
   target clickable → `useOpenItemFile`. Add `if (item.type === "Document") return
   <DocumentItemRenderer item={item} />` to the `ListItemContent.tsx` renderer switch.
6. **i18n** — `lists.attachDocument` already exists (the disabled menu label). Add new keys only if
   the preview-sheet title or renderer needs strings not already present; if a brand-new namespace is
   introduced register it in `src/types/i18next.d.ts` (it won't be — these are existing-namespace
   keys, JSON-only).

No generated-client change is expected: `CreateMediaItem` already takes `type` as a form value and
the TS client was generated against it. Run `npm run api` only if the backend signature actually
shifts.

## Data flow

**Upload:** picker (PDF) → `handleAttachFile` sets `pendingFile` → `MediaPreviewSheet` (filename +
optional caption) → `handleSendMedia` POSTs multipart `{ file, type: "Document", caption }` to
`.../items/media` → slice pre-filters content-type, stores raw bytes (no thumbnail), `AddMediaItem`
→ items query invalidated → `DocumentItemRenderer` appears.

**Open:** tap renderer → `useOpenItemFile` opens a blank tab synchronously, fetches `/file` as an
authed blob, navigates the tab to the object URL; the browser renders the PDF in its native viewer.

## Error handling

Unchanged dispatch via `ResultExtensions`:

- Missing file / disallowed content-type / over the size cap → **400 ValidationProblem**.
- Over the framework body limit → **413** (already wired).
- `IFileStorage` failure mid-upload → compensating blob-delete, then surfaced.
- Item/file not found → **404**; non-member → **404** (the slice's `FindActiveMembershipAsync`
  pre-check returns `NotFound` — existing behavior, unchanged).

## Testing

- **Domain** — `Document` validation already has unit coverage (`ListAggregateMediaItemTests`); no
  new domain test needed beyond confirming the existing ones still pass.
- **Slice** — add a `CreateMediaItem` test for the **document branch**: a PDF upload stores the raw
  blob, sets `ThumbnailStorageKey = null`, and produces a `Document` response. A non-allowed
  content-type returns 400. Use the existing fake in-memory `IFileStorage`.
- **Integration** (Reqnroll + Playwright + Postgres Testcontainers) — mirror the image-item +
  recipe-document ITs: upload a PDF → it renders as a document row (testid) with the filename →
  toggle / reorder / soft-delete behave like any item. Assert on testids / `data-*`, never
  translated text. Remember the IT serves `ClientApp/build` — run `npm run build` after the React
  edits or new testids won't appear.
- Frontend has no JS test runner → manual verification via `/dev-up` + Playwright MCP (open a PDF in
  a new tab, confirm it renders).

## Migration

**None.** All required columns and the enum value already exist on `stage`.

## Dockerfile

No new project, no new NuGet/npm dependency → expected unchanged. Confirm with a final `docker
build` per the verify-with-full-tests-and-docker discipline.

## Docs

Update `knowledge/Lists.md` to record that `Document` items are live (PDF-only, open-in-new-tab,
shares the media pipeline), and remove the sub-feature #3 entry from `IDEAS.md` as the finishing step
once the work ships.
