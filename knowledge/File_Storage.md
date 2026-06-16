# File / Blob Storage & Image Processing

Binary attachments (list-item media, recipe image/PDF attachments) live in blob storage behind a tiny Domain port, not in Postgres. Two providers — local filesystem for dev/test, Google Cloud Storage (Firebase Storage bucket) for prod — selected by config. Uploaded images are normalized and hardened through ImageMagick before storage. Orphaned blobs are swept by a startup maintenance task.

## Ports (`Frigorino.Domain/Interfaces/`)

- `IFileStorage` — `SaveAsync(Stream) → string key` (opaque GUID), `OpenAsync(key) → Stream?` (null if absent), `DeleteAsync(key)` (idempotent). Deliberately lean: no content-type/length stored here — that metadata lives on the owning row (`ListItem`, `RecipeAttachment`).
- `IImageProcessor` — `ProcessAsync(Stream) → Result<ProcessedImage>` where `ProcessedImage` = full-res WebP bytes + thumbnail WebP bytes + shared content-type. Decodes, validates format, auto-orients from EXIF, strips all metadata, re-encodes two renditions.
- `IFileStorageMaintenance` — `ListAsync() → IAsyncEnumerable<StoredBlob>` (key + created-at) for the orphan sweep; scoped to the backend's own namespace only.

## Areas & keying

`Services/BlobAreas.cs` names the areas: `ListItem` (`"list-item"`) and `RecipeAttachment` (`"recipe-attachment"`). Each area is a separately-keyed singleton of `IFileStorage` **and** `IFileStorageMaintenance`. Stored keys are bare GUIDs; the provider composes a namespaced object path `{environment}/{area}/{key}` (`Services/GcsObjectNaming.cs`) so multiple environments can share one bucket without collision.

## Providers & selection

- `Services/LocalFileStorage.cs` — writes under `{LocalPath or ContentRoot/blobs}/{env}/{area}/{guid}`. Validates keys (rejects separators, `..`, absolute paths; must stay within root). Dev/test default.
- `Services/GcsFileStorage.cs` — `Google.Cloud.Storage.V1` against a Firebase Storage bucket, thread-safe `StorageClient` singleton, credential from `FirebaseSettings:AccessJson`.
- `Services/FileStorageDependencyInjection.cs` (`AddFileStorage`) wires the provider per area from `FileStorage:Provider` (`Local` default / `Gcs`).

## Image processing & hardening (`Services/MagickImageProcessor.cs`)

Invoked on every image upload (list-item media, recipe image attachments — PDFs skip it). Validates the format header is JPEG/PNG/WebP, rejects decode-bomb dimensions before a full decode, auto-orients, strips EXIF/IPTC/XMP/ICC, and emits a full-res WebP (~≤2560px, q≈82) + a thumbnail WebP (~≤480px, q≈75). Stateless → singleton.

`Services/ImageProcessingDependencyInjection.cs` (`AddImageProcessing`) applies **defense-in-depth at the native ImageMagick layer** (ImageTragick mitigation): deny *all* delegates and coders, then re-allow only JPEG/PNG/WEBP read+write, plus hard resource limits — ~64 MP header guard, single thread (no OpenMP fan-out), width/height caps, ~256 MP area backstop, ~16-frame list cap, ~256 MB memory cap. These bound any single malicious/huge upload.

## Orphan reclamation

`Tasks/ReclaimOrphanBlobs.cs` is an `IMaintenanceTask` (cold-start, unconditional via `AddMaintenanceServices`). Mark-and-sweep per `IBlobReferenceSource` (`Services/IBlobReferenceSource.cs`): list every blob in the area, load the set of keys referenced by DB rows (including **soft-deleted** ones, so undo still has its blob), and delete blobs that are unreferenced **and** older than a grace period (protects in-flight uploads whose row hasn't committed). The reclaim decision is isolated as pure, unit-testable logic in `Tasks/OrphanBlobSweep.cs` (`SelectReclaimableKeys(blobs, referenced, now, grace)`); the grace period is `MaintenanceSettings:OrphanBlobGraceHours` (default 24h). Reference sources: `Tasks/ListItemBlobReferences.cs` and `Tasks/RecipeAttachmentBlobReferences.cs` (each collects `StorageKey` + `ThumbnailStorageKey` from all rows in its table).

## Writers & orphan-safe ordering

`Features/Lists/Items/CreateMediaItem.cs` and `Features/Recipes/Attachments/CreateRecipeAttachment.cs` both: gate on the entity's `MaxFileSizeBytes`, process (images) / pass through (PDFs), **save blob(s) first, then insert the row**, and best-effort delete the blob(s) if the row insert throws. So the only failure mode is an orphan blob (no row), which `ReclaimOrphanBlobs` cleans up — never a row pointing at a missing blob. Reads stream the blob back with `Content-Type` from the row and a 1-year immutable `Cache-Control` (keys are content-addressable): `Features/Lists/Items/GetItemFile.cs`, `Features/Recipes/Attachments/GetRecipeAttachmentFile.cs` + `GetRecipeAttachmentThumbnail.cs`.

## Config (`FileStorage` section)

| Key | Notes |
|---|---|
| `Provider` | `Local` (dev/test) or `Gcs` (prod). |
| `Bucket` | GCS/Firebase Storage bucket (Gcs only). |
| `Environment` | Env token (`stage`/`prod`) composed into every object prefix. |
| `LocalPath` | Filesystem root (Local only; falls back to `{ContentRoot}/blobs`). |
| `FirebaseSettings:AccessJson` | Service-account credential reused for GCS. |
