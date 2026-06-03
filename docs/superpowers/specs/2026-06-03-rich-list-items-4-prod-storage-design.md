# Rich list items #4 — Production storage backend (GCS) + orphan-blob cleanup — design

- **Date:** 2026-06-03
- **Status:** Approved (design); plan pending
- **Branch:** `feat/rich-list-items-4-prod-storage` (off `feat/rich-list-items`, the long-lived integration branch)
- **Parent:** `2026-05-23-rich-list-items-design.md` (sub-feature **#4** of the decomposition table)
- **Builds on:** `2026-06-03-rich-list-items-2-image-items-design.md` (image items, end-to-end — the `IFileStorage` consumer this makes production-ready)

> **Sequencing note.** The decomposition table orders this as #4 (after #3 documents), but it is being
> done **before #3** by explicit user decision: images are functionally complete on the dev path and
> the only thing standing between them and production use is a persistent storage backend. Documents
> (#3) follow afterward and reuse the same now-production-ready pipeline.

## Summary

Sub-feature #2 took an image all the way through the stack (attach → re-encode + thumbnail → auth'd
byte serving → lightbox → caption editing), but it runs entirely on `LocalFileStorage`, which writes
to the container's local disk. On Railway that disk is **ephemeral** — images uploaded in production
vanish on the next deploy/restart. The `AddFileStorage` DI wiring already anticipates this and points
at this sub-feature as the fix.

This sub-feature makes images **production-ready** by adding two cohesive pieces behind the existing
seam:

1. **`GcsFileStorage`** — a Firebase Cloud Storage (Google Cloud Storage) backend for `IFileStorage`,
   selected by a config switch. The upload/serve slices and the port's hot path are unchanged.
2. **`ReclaimOrphanBlobs`** — a mark-and-sweep maintenance task that reclaims blobs no `ListItems` row
   references. This is newly necessary: the existing purge (`DeleteInactiveItems`) bulk-deletes rows
   (and cascade-deletes through removed lists/households) **without touching blob storage**. Harmless
   while blobs are throwaway dev files; a permanent leak the moment a real paid backend is behind the
   port.

After #4, a household member's uploaded images survive deploys/restarts, and storage cost stays
bounded because purged items' blobs are reclaimed.

## Goals

- Provide a persistent production `IFileStorage` backend (Firebase Cloud Storage / GCS).
- Reuse the existing Firebase service-account credential — no new secret to provision.
- Select the backend by configuration (`Local` for dev/test, `Gcs` for prod) behind the same port.
- Keep proxying bytes through the API (the API stays the authz gatekeeper — parent decision #6).
- Reclaim orphaned blobs via a dedicated maintenance task, robust to every leak path.
- Keep the storage backend swappable; the hot-path port stays lean.

## Non-goals / out of scope

- **Documents (#3).** Unchanged by this sub-feature; they ride the same pipeline once #4 lands.
- **Signed-URL direct transfer.** Still proxying through the API (parent decision #6). Signed URLs
  remain a later optimization behind the same port if egress ever bites.
- **Post-upload classify/analyze hook.** Future background task (parent non-goal).
- **Multi-bucket / per-household bucket partitioning.** One bucket, one key prefix.
- **CDN / edge caching of media.** Out of scope; the long immutable `Cache-Control` from #2 already
  lets browsers cache.
- **Other vendors (R2 / S3).** Firebase/GCS is chosen because the project already has the GCP project
  and credential. The port stays vendor-neutral so a future switch is contained.

## Key decisions (this sub-feature)

1. **Firebase Cloud Storage (GCS) as the production backend.** The project already uses Firebase Auth
   with a service-account JSON; Cloud Storage lives in the same GCP project and authorizes with the
   same credential. Lowest-friction option — no new vendor, no new account. *(User has Firebase/GCP
   set up; chose this over R2/S3.)*
2. **Reuse `FirebaseSettings:AccessJson` for the GCS credential.** The exact credential factory the
   auth path uses — `CredentialFactory.FromJson<ServiceAccountCredential>(AccessJson).ToGoogleCredential()`
   (`FirebaseAuth.cs:21`) — produces the `GoogleCredential` a `StorageClient` needs. No second secret.
3. **Config switch `FileStorage:Provider` (`Local` | `Gcs`).** The `AddFileStorage` DI comment already
   anticipates this switch. Default `Local` keeps dev/test/CI on `LocalFileStorage` (no GCS calls in
   tests). New config `FileStorage:Bucket`.
4. **Prefix-scoped GCS objects for sweep safety (⟐ A).** `GcsFileStorage` writes every object under a
   fixed prefix (e.g. `list-items/{guid}`) and scopes all operations to it. The DB still stores the
   **bare GUID** key (backend-agnostic); the prefix is purely a GCS-impl concern. This guarantees the
   reconciliation sweep can only ever enumerate and delete Frigorino's own objects, even if the
   Firebase bucket is shared with other features. *(Chosen over assuming a dedicated bucket.)*
5. **Interface segregation for the listing capability (⟐ B).** The hot-path port (`SaveAsync` /
   `OpenAsync` / `DeleteAsync`) stays lean. Listing is a separate capability,
   `IFileStorageMaintenance.ListAsync`, implemented by both backends. Upload/serve slices keep
   depending only on `IFileStorage`; only the sweep depends on the maintenance interface. *(ISP — keeps
   the hot path minimal.)*
6. **Reconciliation sweep (mark-and-sweep), not targeted deletion-time delete.** The sweep is robust to
   every leak path — the bulk purge, DB cascade deletes through removed lists/households, and a crash
   between `SaveAsync` and `SaveChangesAsync` — without tracing FK relationships. Self-healing. At
   household scale the full-bucket list is negligible. *(User chose the sweep over a targeted purge-time
   delete or a hybrid.)*
7. **Dedicated maintenance task `ReclaimOrphanBlobs`, not folded into `DeleteInactiveItems`.**
   `MaintenanceHostedService` runs each task in its own DI scope with per-task error isolation, so a
   storage hiccup during the sweep can't abort the DB purge and vice versa. Clean separation of DB-row
   purge from storage GC. *(User chose the dedicated task.)*
8. **Grace period on the sweep (default 24h).** Only blobs older than the grace period are eligible for
   deletion, protecting an in-flight upload whose row isn't committed yet. The age comes from the
   blob's storage-side creation time (GCS `TimeCreated`; local file timestamp). *(Robustness against the
   upload-then-commit race.)*
9. **GCS object content-type is not significant.** The port's `SaveAsync(Stream, ct)` carries no
   content-type by design (#1 dropped it — the served `Content-Type` comes from the DB row). The GCS
   upload uses the default `application/octet-stream`; `GetItemFile`/`GetItemThumbnail` still set the
   real type from `ListItem.ContentType`. No behavior change.

## Architecture

Two pieces, both behind the existing seam in `Frigorino.Infrastructure`:

- **`GcsFileStorage : IFileStorage, IFileStorageMaintenance`** — the production blob backend.
- **`ReclaimOrphanBlobs : IMaintenanceTask`** — the mark-and-sweep GC, registered in
  `AddMaintenanceServices` alongside `DeleteInactiveItems`.

The port, the upload/serve slices (`CreateMediaItem`, `GetItemFile`, `GetItemThumbnail`), the
`ListItem` aggregate, and all DTOs are **unchanged** except the additive `IFileStorageMaintenance`
interface and the new DI/config.

## Domain / Interfaces (`Frigorino.Domain/Interfaces`)

`IFileStorage` is unchanged:

```csharp
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, CancellationToken ct); // returns opaque GUID key
    Task<Stream?> OpenAsync(string key, CancellationToken ct);     // null when missing
    Task DeleteAsync(string key, CancellationToken ct);            // idempotent
}
```

New, segregated listing capability (lives beside `IFileStorage`):

```csharp
public interface IFileStorageMaintenance
{
    // Enumerates every blob this backend owns (its own namespace only), with storage-side creation
    // time for the sweep's grace-period filter.
    IAsyncEnumerable<StoredBlob> ListAsync(CancellationToken ct);
}

public readonly record struct StoredBlob(string Key, DateTimeOffset CreatedAt);
```

- Both `LocalFileStorage` and `GcsFileStorage` implement both interfaces.
- `StoredBlob.Key` is the **bare** key (GUID), matching what the DB stores — the GCS impl strips its
  internal prefix before returning.

## Infrastructure

### `GcsFileStorage` (`Frigorino.Infrastructure/Services`)

- Holds a single `StorageClient` (thread-safe → singleton, like `LocalFileStorage`), the bucket name,
  and the key prefix.
- Construction is **deferred** in the DI factory lambda (see DI below) so DI build / build-time OpenAPI
  generation never opens a client or needs real credentials.
- `SaveAsync(content, ct)`: `key = Guid.NewGuid().ToString("N")`;
  `await client.UploadObjectAsync(bucket, ObjectName(key), contentType: null, content, ...)`; return
  the bare `key`. (`ObjectName(key)` = `$"{prefix}/{key}"`.)
- `OpenAsync(key, ct)`: download into a stream via `DownloadObjectAsync(bucket, ObjectName(key), ms)`;
  return the stream. Catch `GoogleApiException` with `HttpStatusCode.NotFound` → return `null` (port
  contract). (Buffer into a `MemoryStream` to match the existing return-a-readable-stream contract;
  rendition sizes are bounded by #2's caps.)
- `DeleteAsync(key, ct)`: `DeleteObjectAsync(bucket, ObjectName(key))`; ignore 404 (idempotent).
- `ListAsync(ct)`: `client.ListObjectsAsync(bucket, prefix)` → for each object yield
  `new StoredBlob(StripPrefix(obj.Name), obj.TimeCreated ?? DateTimeOffset.MinValue)`. Only the prefix
  is enumerated, so foreign objects in a shared bucket are never seen.

### `LocalFileStorage` additions

- Implements `IFileStorageMaintenance.ListAsync`: enumerate files directly under `_root` (flat GUIDs),
  yielding `new StoredBlob(fileName, File.GetLastWriteTimeUtc(path))`. Blobs are write-once
  (content-addressable GUID, never updated), so last-write time equals creation time and is portable
  across OSes (unlike creation time on Linux). The root dir is the local namespace, so the sweep is
  inherently scoped.
- Hot-path methods (`SaveAsync`/`OpenAsync`/`DeleteAsync`) are unchanged.

### `ReclaimOrphanBlobs` (`Frigorino.Infrastructure/Tasks`)

Runs in the cold-start maintenance batch. Steps:

1. Build the **referenced set**: every non-null `StorageKey` and `ThumbnailStorageKey` across **all**
   `ListItems` rows — active **and** soft-deleted (soft-deleted items retain their blob for undo).
   `_dbContext.ListItems.Where(li => li.StorageKey != null).Select(li => new { li.StorageKey, li.ThumbnailStorageKey })`
   → flatten into a `HashSet<string>`.
2. Enumerate `IFileStorageMaintenance.ListAsync`.
3. For each blob: if its `Key` is **not** in the referenced set **and** `CreatedAt` is older than the
   grace period (now − `OrphanBlobGracePeriod`, default 24h) → `IFileStorage.DeleteAsync(key)`.
4. Log the count (and optionally bytes) reclaimed.

- **Order-independent** with `DeleteInactiveItems`: a row purged this cycle simply has its blob
  reclaimed this-or-next sweep. Registration order doesn't affect correctness.
- The selection logic (referenced-set diff + grace-period filter) is extracted into a **pure static
  function** (mirroring `CheckedItemPurge.SelectExpiredItemIds`) for unit testing without disk or DB:
  `OrphanBlobSweep.SelectReclaimableKeys(IEnumerable<StoredBlob> blobs, ISet<string> referenced, DateTimeOffset now, TimeSpan grace)`.

### Settings

- The grace period is read from config (bound to the existing `MaintenanceSettings` in
  `Frigorino.Infrastructure/Notifications`) with a default of 24h — overridable, not hard-coded in the
  task. No other sweep tunables are needed.

## Dependency injection (`FileStorageDependencyInjection`)

`AddFileStorage` reads `FileStorage:Provider`:

```csharp
var provider = configuration["FileStorage:Provider"]; // "Local" (default) | "Gcs"
if (string.Equals(provider, "Gcs", StringComparison.OrdinalIgnoreCase))
{
    var bucket = configuration["FileStorage:Bucket"];     // required for Gcs
    var prefix = configuration["FileStorage:KeyPrefix"];  // optional, default "list-items"
    // Reuse the Firebase service-account JSON for the GCS credential.
    var accessJson = configuration.GetSection(FirebaseSettings.SECTION_NAME).Get<FirebaseSettings>()?.AccessJson;
    services.AddSingleton<GcsFileStorage>(sp => /* defer StorageClient.Create(credential) here */);
    services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<GcsFileStorage>());
    services.AddSingleton<IFileStorageMaintenance>(sp => sp.GetRequiredService<GcsFileStorage>());
}
else
{
    // existing LocalFileStorage path, also registered for IFileStorageMaintenance
    services.AddSingleton<LocalFileStorage>(sp => /* existing deferred lambda */);
    services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());
    services.AddSingleton<IFileStorageMaintenance>(sp => sp.GetRequiredService<LocalFileStorage>());
}
```

- Both interfaces resolve to the **same** singleton instance (registered concrete, forwarded twice).
- Construction stays deferred in the factory lambdas; missing/empty `Bucket` under `Gcs` fails fast on
  first resolve with a clear message (not at DI build, to keep build-time OpenAPI clean).
- `ReclaimOrphanBlobs` is registered in `AddMaintenanceServices` next to `DeleteInactiveItems`.

## Configuration & deployment

- `appsettings.json` placeholders: `FileStorage:Provider` (empty → `Local`), `FileStorage:Bucket`
  (empty), `FileStorage:KeyPrefix` (optional). Secrets/real values via env vars per the project's
  config convention.
- **Railway** (stage **and** prod, per [[project_railway_vite_build_args]] discipline of setting vars
  in every env): `FileStorage__Provider=Gcs`, `FileStorage__Bucket=<firebase-bucket>`. `AccessJson`
  is already present for auth. Optional `FileStorage__KeyPrefix`.
- The Firebase bucket name is the project's Cloud Storage bucket
  (`<project-id>.appspot.com` or `<project-id>.firebasestorage.app`). The GCP project must be on the
  Blaze plan (Cloud Storage requirement since 2026-02-03; stays $0 under the legacy free tier — see the
  parent spec's "Limits & cost"). Operator action, not code.
- No EF migration.

## Error handling

- GCS missing object → `OpenAsync` returns `null` (→ existing 404 path in `GetItemFile`/`GetItemThumbnail`);
  `DeleteAsync` ignores 404.
- GCS transient/credential failure during a slice → bubbles to existing middleware → 500; #2's
  compensating-delete on upload-persist failure is unchanged.
- Sweep failures are caught per-task by `MaintenanceHostedService` (logged, never crash startup).
- A blob `DeleteAsync` failure mid-sweep is logged and the sweep continues (best-effort; next run
  retries).

## Testing

- **`OrphanBlobSweep.SelectReclaimableKeys` unit tests** (pure, no disk/DB): referenced key kept;
  unreferenced + aged key reclaimed; unreferenced + fresh key kept (grace period); thumbnail keys
  counted as referenced; empty inputs.
- **`LocalFileStorage.ListAsync` impl test**: write blobs, assert keys + timestamps enumerated; keys
  are bare GUIDs.
- **`GcsFileStorage`**: unit-test the prefix mapping (`ObjectName`/`StripPrefix`) and the 404→null
  mapping where the client boundary can be faked; **no live GCS in CI**. (Full GCS exercise is manual /
  out-of-band.)
- **Integration** (Reqnroll + Postgres Testcontainers) with `LocalFileStorage` bound to a temp dir:
  seed a referenced blob, an aged orphan, and a fresh orphan; run `ReclaimOrphanBlobs`; assert only the
  aged orphan is gone and the referenced + fresh blobs remain. Reuse the #2 upload→serve scenario to
  confirm the provider switch path doesn't regress the `Local` default.
- Frontend unaffected — no JS changes; no manual browser pass required beyond a smoke check that
  uploads still work on the `Local` default.

## Dependencies & Dockerfile

- **NuGet:** `Google.Cloud.Storage.V1` added to `Frigorino.Infrastructure`, exact-pinned per the
  project's pinning policy. No new project.
- **Migration:** none.
- **Dockerfile:** expected unchanged (NuGet only); confirm with `docker build`, and run the full
  `dotnet test Application/Frigorino.sln` gate before merging back to `feat/rich-list-items`.
