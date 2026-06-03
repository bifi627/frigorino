# Rich list items #2 — Image items, end-to-end — design

- **Date:** 2026-06-03
- **Status:** Approved (design); plan pending
- **Branch:** `feat/rich-list-items-2-image-items` (off `feat/rich-list-items`, the long-lived integration branch)
- **Parent:** `2026-05-23-rich-list-items-design.md` (sub-feature **#2** of the decomposition table)
- **Builds on:** `2026-06-03-rich-list-items-1-foundation-design.md` (typed-item foundation + storage seam)

## Summary

Sub-feature #1 landed the seam: `ListItemType`, the five nullable media columns + one migration,
`List.AddMediaItem`, the lean `IFileStorage` port, and `LocalFileStorage`. It was deliberately
endpoint-free.

This sub-feature is the **walking skeleton**: it takes an **image** all the way through the stack —
attach → upload → server-side re-encode + thumbnail → auth'd byte serving → list render → full-res
lightbox — so the whole pipeline is proven and demoable. The **document** type stays scaffolded but
inert (a disabled menu entry); sub-feature #3 flips it on by reusing this pipeline wholesale.

After #2, a user can take a photo, attach it with an optional caption, see a thumbnail in the list,
tap to view full-res, and toggle / reorder / delete / restore it like any other item.

## Goals

- Upload an image item end-to-end behind the existing `IFileStorage` seam.
- Re-encode every stored image (full-res + thumbnail) to strip metadata and normalize format.
- Serve image bytes only through the authenticated API (the API stays the authz gatekeeper).
- Render thumbnails cheaply in the list; full-res only on tap (lightbox).
- Keep the generic Composer's single responsibility intact; media preview is list-owned.
- Keep the image library swappable behind a small port (like the AI classifier/extractor ports).

## Non-goals / out of scope

- **Documents (#3).** No document upload/render in #2; the menu entry is present but disabled.
- **Production storage backend (#4).** `LocalFileStorage` (dev) only; prod vendor is a separate track.
- **Signed-URL direct transfer.** v1 proxies bytes through the API (parent decision #6).
- **In-app editing** (crop/rotate UI). We auto-orient + re-encode server-side; users upload as-is.
- **Orphaned-blob cleanup / post-upload classify.** Future background tasks (parent non-goals).
- **Multi-file / batch upload.** One image per attach action (matches expected low volume).

## Key decisions (this sub-feature)

1. **Re-encode + strip EXIF on the stored full-res, not just the thumbnail.** Phone photos carry GPS
   EXIF; serving the original would leak location to every household member and serve untrusted-origin
   bytes. We auto-orient, drop **all** metadata, and re-encode both renditions to WebP. *(User chosen
   over "store original as-uploaded".)*
2. **`IImageProcessor` port (Domain), ImageSharp impl (Infrastructure), kept deliberately small.**
   One method; the slice depends on the abstraction and is unit-tested with a fake. Mirrors the
   `IItemClassifier` / `IQuantityExtractor` / `IFileStorage` pattern so the library is swappable
   (Magick.NET / SkiaSharp) if ImageSharp ever bites. *(User: "keep the interface small so we can
   switch later, similar to the AI decision.")*
3. **ImageSharp as the v1 image library.** Six Labors Split License — free for Frigorino's scale;
   the small port above contains the dependency. *(User confirmed.)*
4. **`IFormFile` over manual `MultipartReader` streaming.** ImageSharp materializes the decoded image
   regardless, so streaming saves no peak memory for images; `IFormFile` spools >64 KB to a temp file
   (not RAM), and — critically — auto-declares the multipart schema so **hey-api generates a typed
   uploader** (no hand-rolled `mutationFn`). Expected volume is ~1 image at a time. *(User confirmed.)*
5. **Stored `ContentType` is the processed output (`image/webp`), not the client's claim.** The wire
   content-type is never trusted; the DB records what we actually wrote. Because every stored image is
   WebP, the aggregate's `ListItem.ImageContentTypes` allowlist becomes a defense-in-depth check that
   always sees `image/webp` (already allowlisted from #1). The **real input-format gate moves into the
   processor**: it accepts only **JPEG / PNG / WebP** decoded input and rejects any other format
   ImageSharp could otherwise decode (gif/bmp/tga/…) → 400. This shrinks the decode attack surface and
   avoids surprises (e.g. animated GIF → WebP). Configure ImageSharp's enabled decoders / validate the
   detected format accordingly.
6. **Preview + caption + send is a list-owned sheet, not a Composer feature.** The Composer is shared
   with Inventories; baking image preview into its generic feature system would overload it. The
   attach affordance is a thin Composer **action-feature** that only signals "user picked a photo";
   the list owns the preview/caption/send UI. *(Keeps single responsibility — see clean-separation.)*
7. **Thumbnail ~480px WebP; full-res ~2560px WebP.** 480px covers the list row at 3× retina with
   headroom; full-res is capped so storage/egress stay bounded while looking sharp on any screen.
   *(User chose 480px over 320px.)*

## Domain

`ListItem` and `List.AddMediaItem` are unchanged from #1 — they already accept a complete
`StoredFile` and map caption → `Comment`. #2 adds **no domain code**, only the image-processing port.

### `IImageProcessor` (Frigorino.Domain/Interfaces)

```csharp
public interface IImageProcessor
{
    // Decodes (validating it is a real image), auto-orients, strips all metadata, and re-encodes
    // both renditions. Returns Fail (mapped to 400) when the input is not a decodable image.
    Task<Result<ProcessedImage>> ProcessAsync(Stream input, CancellationToken ct);
}

// Bytes + the single content-type we actually wrote, for both renditions.
public sealed record ProcessedImage(
    byte[] FullRes, byte[] Thumbnail, string ContentType, long FullResSizeBytes);
```

- Lives beside `IFileStorage` / `ICurrentUserService`. Impl in Infrastructure, DI-wired.
- Encoding parameters (full-res cap ~2560px @ ~q82, thumbnail ~480px @ ~q75, WebP) are
  **Infrastructure** constants, not Domain — they are rendering policy, not an aggregate invariant.
- Returns `byte[]` (not streams): renditions are already fully in memory after decode; the expected
  ~1-at-a-time, ≤25 MB volume makes buffering a non-issue and keeps the port trivially fakeable.

## API surface (vertical slices, `Frigorino.Features/Lists/Items/`)

### `CreateMediaItem` — `POST /api/household/{householdId}/lists/{listId}/items/media`

Multipart. Request body (drives hey-api codegen): `file` (binary, `IFormFile`), `type`
(`ListItemType`, string on wire — `Image` in #2), `caption` (string, optional). #2 processes the
upload as an image; the `type` field is carried now so #3 can branch the slice (image → re-encode,
document → pass-through) without changing the contract. The menu's Document option is disabled in #2,
so the client only ever sends `Image`; a `Document` upload would naturally 400 against the per-type
allowlist + thumbnail invariant until #3 implements the branch.

1. Authorize household membership (`FindActiveMembershipAsync`) → 404 if not a member.
2. Load the list (`Include(ListItems)`, scoped to household, active) → 404 if missing.
3. `IImageProcessor.ProcessAsync(file.OpenReadStream())` → `Result<ProcessedImage>`; undecodable → 400.
4. `IFileStorage.SaveAsync(fullRes)` → storage key; `SaveAsync(thumbnail)` → thumbnail key.
   **Uploads happen before the DB write** (parent orphan-safe ordering).
5. `list.AddMediaItem(ListItemType.Image, caption, new StoredFile(storageKey, thumbnailKey,
   processed.ContentType, file.FileName, processed.FullResSizeBytes))` → `ValidationProblem` on failure.
6. `SaveChangesAsync`. **If it throws, compensating-delete both just-saved blobs** before rethrowing —
   no dangling row, no orphan blob.
7. Return `201 Created` with `ListItemResponse`.

Transport guard: endpoint sets a max body size just above the 25 MB cap → **413** before the body is
fully read. `DisableAntiforgery()` as needed for the minimal-API multipart endpoint.

### `GetItemFile` — `GET .../items/{id}/file` & `GetItemThumbnail` — `GET .../items/{id}/thumbnail`

1. Authorize membership → 404/403.
2. Load the item scoped to list + household + active → 404 if missing or the relevant key is null.
3. `IFileStorage.OpenAsync(key)` → 404 if the blob is gone.
4. Stream the bytes with `Content-Type` + `FileSizeBytes` **read from the DB row** (the #1 port is
   lean — no content-type/length in the port, which is exactly why those columns exist), a long
   `Cache-Control` (immutable; keys are content-addressable GUIDs), and a `Content-Disposition`
   filename sanitized from `OriginalFileName`.

`GetItemThumbnail` uses `ThumbnailStorageKey`; `GetItemFile` uses `StorageKey`.

### `ListItemResponse` additions

Gains `Type` (`ListItemType`), `FileName` (`OriginalFileName`), `ContentType`, `FileSize`
(`FileSizeBytes`). Caption already flows through the existing `Comment` field. Add to **both**
`From(...)` and the EF `ToProjection` expression. The client derives `/file` and `/thumbnail` URLs
from `householdId` + `listId` + `item.id`.

Existing slices (`CreateItem`, `UpdateItem`, `ToggleItemStatus`, `ReorderItem`, `DeleteItem`,
`RestoreItem`, `CompactItems`, `GetItems`, `GetItem`) are unchanged except inheriting the DTO fields.

## Data flow

### Upload
Browser picks a photo → list-owned preview sheet (local `object-URL` preview + caption) → Send posts
multipart (file + `type=Image` + caption) with the Firebase bearer → `CreateMediaItem` processes,
stores blobs, persists (orphan-safe + compensating delete) → returns the DTO → client invalidates the
items query (no optimistic insert; the sheet shows upload progress).

### Render / download
`GetItems` returns `type` + metadata → `ImageItemRenderer` fetches `/thumbnail` through the
**configured auth client** as a Blob → `URL.createObjectURL` → `<img src>` (a plain `<img src>`
cannot carry the bearer). Cached via TanStack Query; object URLs revoked on unmount. Tap → lightbox
fetches `/file` the same way.

## Frontend

- **Renderer switch keyed by `item.type`.** Extract today's `ListItemContent` text logic into
  `TextItemRenderer` (unchanged behavior, incl. URL auto-linkify); add `ImageItemRenderer`
  (thumbnail + caption; tap → lightbox; long-press copy stays text-only). `DocumentItemRenderer`
  is **#3**.
- **Attach affordance** — a Composer **action-feature** (`renderTrigger`) rendering a "+" that opens
  a small menu: **Photo** (live) and **Document** (disabled "coming soon"). Photo triggers a hidden
  `<input type="file" accept="image/*" capture>`; on pick it `complete({...})`s up to the list, which
  opens the preview sheet. Registered for Lists only (like `quantityComposerFeature`).
- **`MediaPreviewSheet`** (list-owned) — local-file `object-URL` preview, caption `TextField`,
  Send / Cancel. Send calls `useCreateMediaItem`; shows upload progress; closes + invalidates on success.
- **`useCreateMediaItem`** — arg-less; spreads the generated `createMediaItemMutation()` (multipart via
  hey-api's `formDataBodySerializer`). Caller passes `{ path, body: { file, type, caption } }`.
  Invalidates the items query in `onSuccess`. **Do not** set `Content-Type` manually (the browser sets
  the multipart boundary). Standard one-hook-per-file shape.
- **`useItemImage(item, variant)`** — auth-blob hook. GETs `/thumbnail` | `/file` via the configured
  `client` as a Blob → `object-URL`; revoked on unmount; cached by TanStack Query keyed on item id +
  variant. **Justified deviation** from the generated-JSON-hook convention (binary + object-URL
  lifecycle is not what the codegen models — the parent spec anticipated this).
- **Lightbox** — MUI `Dialog`/backdrop showing full-res via `useItemImage(item, "file")`, with a
  loading state and the caption.

## Error handling

Reuses `ResultExtensions` dispatch:
- Disallowed `type` / content-type, over-size, caption too long, **undecodable image** →
  **400 ValidationProblem** (`AddMediaItem` + the processor's `Fail`).
- Oversize request body → **413** (transport guard, before full read).
- `IFileStorage` / processing failure mid-save → **500**, with compensating blob-delete.
- Item / blob not found → **404** (`EntityNotFoundError`); non-member → **403**.

## Testing

- **Slice tests** (xUnit + FakeItEasy) with a fake `IFileStorage` **and** fake `IImageProcessor` — no
  disk, no ImageSharp: `CreateMediaItem` happy path (blobs saved before persist, DTO shape), DB-failure
  compensation (both blobs deleted), undecodable image → 400, non-member → 404; `GetItemFile` /
  `GetItemThumbnail` content-type + 404/403 paths.
- **`IImageProcessor` (ImageSharp) impl test** — feed a tiny real PNG: asserts WebP output, both
  renditions present, thumbnail longest edge ≤ ~480px, metadata stripped, garbage bytes → `Fail`.
- **Integration** (Reqnroll + Playwright + Postgres Testcontainers) binds `LocalFileStorage` to a temp
  dir: upload a photo (Playwright file-chooser) → thumbnail in the list → open lightbox; plus an
  API-level scenario POST multipart → GET `/file` + `/thumbnail` (200 + `image/webp`); then toggle /
  reorder / delete / **restore** a media item (restore re-exposes the same keys). Assertions on
  testids / `data-*` only — never translated text.
- Frontend has no JS test runner → manual verification via `/dev-up` + Playwright MCP.

## Dependencies & Dockerfile

- **NuGet:** `SixLabors.ImageSharp` added to `Frigorino.Infrastructure` (NuGet exact-pinned per the
  project's pinning policy). No new project.
- **Migration:** none — #1 added all media columns; `image/webp` is already allowlisted.
- **Dockerfile:** expected unchanged (no new project); confirm with `docker build` at the end, and run
  the full `dotnet test Application/Frigorino.sln` gate before merging back to `feat/rich-list-items`.
