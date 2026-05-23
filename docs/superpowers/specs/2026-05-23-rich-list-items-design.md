# Rich list items (text / image / document) — design

- **Date:** 2026-05-23
- **Status:** Approved (design); implementation plan pending
- **Branch:** `feat/rich-list-items` (off `stage`)

## Summary

List items are text-only today (`Text` + `Quantity` + `Status`). Users want to attach
**photos they took** and **documents** (e.g. a PDF warranty/manual) to a list. The item must
still behave like every other list item: checked off, reordered, soft-deleted and restored.

This feature makes `ListItem` a **typed** item — `Text`, `Image`, or `Document` — where image and
document items carry an uploaded file instead of (or alongside) text. The file pipeline (upload,
storage, download, lifecycle) is a single shared mechanism behind a vendor-neutral `IFileStorage`
port; only the rendering and the content-type/thumbnail edges differ per type.

## Goals

- Add `Image` and `Document` item types that upload and store a real file (not a URL).
- Reuse the existing item behaviors unchanged: status toggle, reorder, compact, soft-delete, restore.
- Keep the database model flat and maintainable — one table, no entity inheritance.
- Keep the storage backend swappable; the production vendor is decided/wired separately.
- Generate image thumbnails for cheap list rendering.

## Non-goals / out of scope (follow-up tasks)

- **Production storage backend.** v1 ships `IFileStorage` + a `LocalFileStorage` dev backend so the
  feature is fully demoable and testable locally. Binding a production backend
  (Firebase-GCS / Cloudflare R2 / S3) is a **separate follow-up task** and a prerequisite for prod use.
- **Post-upload classify/analyze.** A future backend-only step (Hangfire job, mirroring the existing
  `ClassificationService` / `ClassifyListsJob`) that reads the stored blob and writes classification
  metadata. The architecture leaves room; it is not built here.
- **Orphaned-blob cleanup job.** Blobs are retained on soft-delete (so undo works) and never
  auto-purged in v1. Reclaiming blobs of permanently-gone items is a future Hangfire job.
- **In-app media transformation** (cropping, EXIF editing, PDF page previews). Users upload as-is.
- **Signed-URL direct transfer.** v1 proxies bytes through the API; signed URLs remain a later
  optimization behind the same `IFileStorage` port if egress ever bites.

## Dependencies

- **Undo-delete / `List.RestoreItem` is not in `stage` yet.** It lives on `feat/undo-delete`
  (unmerged). This branch is based on `stage`, so `RestoreItem` is currently **absent** here. The
  blob-retention decision below is independently valid (it only relies on `RemoveItem`, which exists),
  but the "restore re-exposes the blob" behavior and the restore integration test are **contingent on
  `feat/undo-delete` landing in `stage` first** (then rebase this branch) — or on adding `RestoreItem`
  here. Resolve the ordering before implementing the restore-related work.

## Key decisions & rationale

1. **Items can BE a photo/document, not just HAVE attachments.** A media item is a first-class row
   whose primary content is the file; it still has a checkbox and sort order. (User chose this over
   "text item with attachments".)
2. **One flat `ListItems` table, no EF inheritance.** `Type` is a plain enum column; media fields are
   nullable columns on the same table. No TPH discriminator, no TPT joins, no `Derived : Base`.
   *Rationale: the user has had bad experiences with entity inheritance producing complicated schemas.*
3. **Explicit `Type ∈ {Text, Image, Document}`** rather than `{Text, File}` + derive-from-MIME.
   *Rationale: the frontend uses a distinct renderer per type, so an explicit type makes renderer
   selection a clean `switch` instead of MIME-sniffing in the UI; costs zero extra schema.*
4. **Coupled file pipeline, branched only at the edges.** Storage, upload transport, download, and
   lifecycle are identical for image and document; only the content-type allowlist + thumbnail
   generation (server) and the renderer + capture affordance (client) differ.
   *Rationale: divergence is tiny and there is no in-app transformation; splitting later is cheap
   because `IFileStorage` already isolates storage.*
5. **Object storage via the existing Firebase vendor, but the API stays the authz gatekeeper.**
   Household membership lives in Postgres (`UserHousehold` roles, `ICurrentHouseholdService`), which
   Firebase Storage security rules cannot evaluate. So the browser must not talk to storage on its
   own authority.
6. **Proxy bytes through the API** (browser → API → storage) rather than signed URLs. Simplest correct
   thing, keeps the port truly vendor-neutral (`Put`/`Get`/`Delete` streams), no bucket CORS / TTL.
7. **Vendor-neutral seam, production vendor decided later.** v1 commits to no specific backend in the
   spec; `IFileStorage` + `LocalFileStorage` (dev) is enough to build and test end-to-end.
8. **Thumbnails in v1** (ImageSharp) to keep list-view egress small.
9. **Blob retained on soft-delete.** `RemoveItem` only flips `IsActive`; the blob stays. This keeps
   media items recoverable so the in-flight undo-delete feature can re-expose the same key on restore.
   (See Dependencies — `RestoreItem` itself is not yet in `stage`.)

## Domain model

Single flat table. `ListItem` gains:

| Field | Type | Notes |
|---|---|---|
| `Type` | `ListItemType` enum | `Text` (default) \| `Image` \| `Document`. Serialized as string (global `JsonStringEnumConverter`). Existing rows → `Text`. |
| `Text` | `string?` | Required for `Text` items; optional **caption** for media items. |
| `StorageKey` | `string?` | Opaque key returned by `IFileStorage`. Media items only. |
| `ThumbnailStorageKey` | `string?` | Image items only. |
| `OriginalFileName` | `string?` | For download filename + document card. |
| `ContentType` | `string?` | Stored MIME type. |
| `FileSizeBytes` | `long?` | For display + validation echo. |

Length/limit constants live on `ListItem` (mirroring the existing `TextMaxLength` pattern):
content-type allowlist, max file size, max caption length, max filename length.

### Storage seam — `IFileStorage` (in `Frigorino.Domain/Interfaces`)

```csharp
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct); // returns opaque key
    Task<FileDownload?> OpenAsync(string key, CancellationToken ct);                   // stream + contentType + length
    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed record FileDownload(Stream Content, string ContentType, long Length);
```

- Lives beside `ICurrentUserService` etc. so `Features` depend on the abstraction; impl in `Infrastructure`, wired via DI.
- v1 impl: `LocalFileStorage` (writes under a configured directory; wired in the Development / dev-up path).
- Keys are GUID-based, **not** tied to the DB id (enables upload-before-persist with compensation).

### Aggregate methods on `List`

Item operations already live on the `List` aggregate. Keep `AddItem` for text; add:

```csharp
Result<ListItem> AddMediaItem(
    ListItemType type, string? caption,
    string storageKey, string? thumbnailKey,
    string contentType, string originalFileName, long sizeBytes);
```

- Validates content-type allowlist, size cap, caption/filename lengths (returns `ValidationProblem`-mapped errors).
- Places the new item in the unchecked section exactly like `AddItem` (same sort-order logic).
- Stays pure: the slice performs all I/O and hands it the resulting keys.

`UpdateItem` is extended so a media item's caption can be edited. `ToggleItemStatus`, `ReorderItem`,
`CompactItems`, `RemoveItem` are unchanged and apply uniformly. `RestoreItem` applies uniformly too,
once it exists here (see Dependencies).

## API surface (vertical slices)

List items are already vertical slices under `Frigorino.Features/Lists/Items/`. New/changed slices:

- **`CreateMediaItem`** — `POST .../items/media` (multipart). Authorizes via household-membership
  services, streams the upload with `MultipartReader` (no `IFormFile` buffering), validates
  content-type + size, generates a thumbnail for images, saves blob(s) via `IFileStorage`, calls
  `List.AddMediaItem`, persists. Endpoint declares `multipart/form-data` + disables form-value model
  binding + sets an explicit max body size.
- **`GetItemFile`** — `GET .../items/{id}/file` — streams the full blob with `Content-Type` and a long
  `Cache-Control`.
- **`GetItemThumbnail`** — `GET .../items/{id}/thumbnail` — streams the thumbnail (images only).
- **`ListItemResponse`** gains `type`, `caption`, `fileName`, `contentType`, `fileSize`.
- Existing `CreateItem` (text), `UpdateItem`, `ToggleItemStatus`, `ReorderItem`, `DeleteItem`,
  `RestoreItem`, `CompactItems`, `GetItems`, `GetItem` — unchanged except the DTO additions.

## Data flow

### Upload (orphan-safe ordering)

1. Browser POSTs multipart (file + `type` + optional `caption`) with the Firebase bearer token.
2. Slice authorizes (household membership).
3. Stream file → validate content-type + size → if image, generate thumbnail (ImageSharp).
4. `IFileStorage.SaveAsync` to a fresh GUID key (and thumbnail key) — **before** persisting.
5. `List.AddMediaItem(...)` records metadata → `SaveChanges`.
6. **If the DB save throws, compensate by deleting the just-uploaded blob(s)** — no dangling row, no
   orphan blob.
7. Return the item DTO; frontend invalidates the items query (no optimistic insert for uploads; show
   upload progress instead).

### Render / download

1. `GetItems` returns items with `type` + metadata.
2. Image items render a thumbnail; the thumbnail/full-res bytes are fetched through the **configured
   auth fetch client** → `URL.createObjectURL` → set as the `img` src (a plain `<img src>` cannot
   carry the bearer token). Blobs cached via TanStack Query; object URLs revoked on unmount.
3. Tap on image → full-res lightbox (fetch `/file`). Tap on document → open/download.

## Frontend

- **Shared row shell** (checkbox, drag handle, overflow menu, swipe-to-delete/undo) is identical for
  all types.
- **Renderer switch keyed by `item.type`:** `TextItemRenderer` (current behavior, incl. URL
  auto-linkify), `ImageItemRenderer` (thumbnail + caption; tap → full-res; long-press → copy/share),
  `DocumentItemRenderer` (file card: icon + filename + size; tap → open/download).
- **Add affordance — WhatsApp-style:** the existing plain text input stays the default; an attach/“+”
  button reveals media options: **Text** (current inline type-and-enter), **Photo** (camera/library
  on mobile via `capture`, `accept="image/*"`), **Document** (file picker). 
- **Hooks** follow the one-hook-per-file convention. `useCreateMediaItem` is likely a **justified
  hand-written multipart `mutationFn`** — the hey-api generated client is JSON-oriented; confirm
  against hey-api docs whether a `multipart/form-data` binary property generates a usable uploader
  before hand-rolling. A small auth-aware blob hook backs thumbnail/full-res fetches.

## Error handling

Reuses the existing `ResultExtensions` dispatch:

- Missing file / disallowed content-type / over size cap / caption too long → **400 ValidationProblem**
  (domain validation in `AddMediaItem`).
- Transport guard: explicit max request body size → **413** before the body is fully read.
- `IFileStorage` failure → **500**, with compensating blob-delete.
- Item/file not found → **404** (`EntityNotFoundError`); non-member → **403**.

## Limits & cost (verified 2026-05-23)

- **Firebase Cloud Storage now requires the Blaze plan** (effective 2026-02-03; Spark returns
  402/403). Blaze stays **$0** under the legacy default-bucket free tier: **5 GB stored**,
  **1 GB/day downloaded**, 20K uploads/day, 50K downloads/day. *5 GB storage is the real ceiling.*
- **Railway** has no hard egress cap — $0.05/GB from the $5/mo Hobby credit. Proxying counts download
  egress twice (storage→API, API→browser); negligible at household scale.
- **Mitigations baked into v1:** per-file size cap at the slice boundary; image thumbnails for list
  rendering (full-res only on tap) keep both the 1 GB/day storage egress and Railway egress small.
- These limits are sufficient for a household app. Confirm before prod: (1) Firebase project on Blaze,
  (2) acceptance of the small Railway egress cost. (Both belong to the production-backend follow-up.)

## Testing

- **Domain unit tests** (xUnit + EF InMemory): `AddMediaItem` validation; lifecycle retains the blob
  through soft-delete/restore; toggle/reorder/compact behave identically for media items.
- **Slice tests** use a fake in-memory `IFileStorage` — no disk, no Firebase.
- **Integration tests** (Reqnroll + Playwright + Postgres Testcontainers) bind `LocalFileStorage` to a
  temp dir: upload a photo → thumbnail in list → open full-res; upload a PDF → download; then
  toggle / reorder / delete a media item (restore once `RestoreItem` is present — see Dependencies).
  Assertions on testids / `data-*`, never translated text.
- Frontend has no test runner → manual verification via `/dev-up` + Playwright MCP.

## Migration

One EF migration: add `Type` (default `Text` for existing rows) + the five nullable file columns to
`ListItems`. No inheritance mapping, no new table. Match existing column conventions; only add
`HasMaxLength` where it reflects a real constraint.

## Dockerfile

ImageSharp is a NuGet package and no new project is added, so the Dockerfile is expected to be
unchanged — confirm with a `docker build` at the end of implementation.
