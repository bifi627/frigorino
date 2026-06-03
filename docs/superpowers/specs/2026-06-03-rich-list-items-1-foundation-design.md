# Rich list items #1 — Typed-item foundation + storage seam — design

- **Date:** 2026-06-03
- **Status:** Approved (design); implementation plan pending
- **Branch:** `feat/rich-list-items` (off `stage`)
- **Parent spec:** [`2026-05-23-rich-list-items-design.md`](2026-05-23-rich-list-items-design.md) — see its
  "Decomposition / sequencing" section. This is **sub-feature #1** of four.

## Summary

The first of the four sequenced sub-features that make `ListItem` a typed item (`Text` | `Image` |
`Document`). This one delivers the **foundation and the storage seam only**: the typed-item schema,
the `IFileStorage` port + a `LocalFileStorage` dev backend, and the `List.AddMediaItem` aggregate
method. It is **backend-only and endpoint-free** — everything #2 (image items, end-to-end) needs to
plug into, with nothing user-visible yet.

The walking-skeleton strategy puts this seam first so it is fully unit-testable in isolation before
any HTTP/multipart/ImageSharp/frontend work lands on top of it.

## Goals

- Add the `ListItemType` enum + the nullable file-metadata columns on the existing flat `ListItems`
  table (one EF migration, no inheritance).
- Add `List.AddMediaItem(...)` with full domain validation, mirroring the existing `AddItem` idiom.
- Introduce a vendor-neutral `IFileStorage` port and a working `LocalFileStorage` dev implementation,
  wired via a config-driven DI extension.
- Prove the whole foundation with domain unit tests + a storage round-trip test.

## Non-goals (land in later sub-features)

- **All HTTP slices** — `CreateMediaItem`, `GetItemFile`, `GetItemThumbnail` (sub-feature #2).
- **ImageSharp + thumbnail generation** and multipart transport (sub-feature #2).
- **`ListItemResponse` DTO additions** (sub-feature #2).
- **All frontend** — renderers, attach affordance, hooks (sub-features #2/#3).
- **Production storage backend** — provider-switching behind `IFileStorage` (sub-feature #4).
- Post-upload classify hook, orphaned-blob cleanup (future, per parent spec).

## Decisions carried in from brainstorming (2026-06-03)

1. **Caption reuses the existing `Comment` column** — *not* a new column and *not* overloading `Text`.
   A caption is free-text human prose attached to an item, exactly what `Comment` already models.
   Honors the project's clean-domain-separation principle (a field shouldn't change meaning by mode)
   without growing the schema. For text items `Comment` = hint; for media items `Comment` = caption.
   `Text` keeps a single meaning (the text item's text) and is `""` for media rows.
2. **File metadata travels as a `StoredFile` value object**, not 5 loose positional params — mirrors
   the existing `Quantity` VO passed to `AddItem`.
3. **`IFileStorage` is a lean bytes-only port** — content-type and length already live in DB columns,
   so the store only moves bytes. More honestly vendor-neutral (never relies on the backend
   storing/echoing metadata) and yields the simplest test fake.

## Domain model

### `ListItemType` enum

`Domain/Entities/ListItemType.cs` (enums live with their entity, per `HouseholdRole`):

```csharp
public enum ListItemType
{
    Text = 0,     // default; existing rows backfill to this
    Image = 1,
    Document = 2,
}
```

Serialized as its **string name** on the wire (global `JsonStringEnumConverter`), stored as **int**
in Postgres (EF default — no converter, no migration beyond the column).

### New columns on `ListItem`

All **nullable**; flat table, no inheritance. Caption is **not** here — it reuses `Comment`.

| Field | Type | Notes |
|---|---|---|
| `Type` | `ListItemType` | `int` column, default `0` (`Text`), required. Backfills existing rows. |
| `StorageKey` | `string?` | Opaque key from `IFileStorage`. Media items only. |
| `ThumbnailStorageKey` | `string?` | Image items only (set by #2's thumbnail step). |
| `OriginalFileName` | `string?` | For download filename + document card. |
| `ContentType` | `string?` | Stored MIME type (read back on download in #2). |
| `FileSizeBytes` | `long?` | Display + validation echo. |

`Text` stays `NOT NULL`; media rows store `""`. `Comment` (existing, `≤ CommentMaxLength = 500`)
holds the optional caption.

### New constants on `ListItem`

Source-of-truth `public const`/`static readonly`, read by both the aggregate and EF config (existing
pattern). Proposed v1 values — tunable:

- `MaxFileSizeBytes = 25 * 1024 * 1024` (25 MB).
- `OriginalFileNameMaxLength = 255`, `ContentTypeMaxLength = 255`, `StorageKeyMaxLength = 200`.
- `ImageContentTypes = { "image/jpeg", "image/png", "image/webp" }`.
- `DocumentContentTypes = { "application/pdf" }`.

*HEIC and office formats are deliberately excluded from v1 — HEIC complicates #2's ImageSharp
thumbnailing. The allowlists are constants, trivially extended later.*

## `StoredFile` value object

`Domain/Files/StoredFile.cs` (VOs get a dedicated namespace folder, per `Domain/Quantities/`):

```csharp
public sealed record StoredFile(
    string StorageKey,
    string? ThumbnailKey,
    string ContentType,
    string OriginalFileName,
    long SizeBytes);
```

## Aggregate method — `List.AddMediaItem`

```csharp
Result<ListItem> AddMediaItem(ListItemType type, string? caption, StoredFile file)
```

Pure (no I/O — the #2 slice performs the upload and hands in the resulting keys). Returns
`Result.Fail` with `"Property"` metadata for each validation error (dispatched to `ValidationProblem`
by the existing `ResultExtensions`). Validations:

- `type` must be `Image` or `Document` — reject `Text` (that path is `AddItem`).
- `file.ContentType` must be in the **type-specific** allowlist (`ImageContentTypes` for `Image`,
  `DocumentContentTypes` for `Document`).
- `file.StorageKey` required, non-empty, `≤ StorageKeyMaxLength`.
- `file.OriginalFileName` required, non-empty, `≤ OriginalFileNameMaxLength`.
- `file.SizeBytes` in `(0, MaxFileSizeBytes]`.
- **Type/thumbnail invariant:** `Image` *requires* `file.ThumbnailKey`; `Document` *forbids* it.
- Caption validated via the existing `ValidateComment` helper (`≤ CommentMaxLength`).

On success: append to the **unchecked** section using the same `ComputeAppendSortOrder(false)` logic
as `AddItem`; set `Type`, `Text = ""`, `Comment = NormalizeComment(caption)`, the five file columns,
`Status = false`, timestamps, `IsActive = true`; add to `ListItems`; return the item.

`ToggleItemStatus`, `ReorderItem`, `CompactItems`, `RemoveItem`, `RestoreItem`, `UpdateItem` are
**unchanged** and apply to media items uniformly. (Caption edits via `UpdateItem`'s existing
`comment` parameter come for free; no signature change needed in #1.)

## Storage seam — `IFileStorage`

`Domain/Interfaces/IFileStorage.cs` (beside `ICurrentUserService` etc., so `Features` depend on the
abstraction):

```csharp
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, CancellationToken ct); // returns opaque GUID key
    Task<Stream?> OpenAsync(string key, CancellationToken ct);     // null if the key is absent
    Task DeleteAsync(string key, CancellationToken ct);            // idempotent (no-op if absent)
}
```

### `LocalFileStorage` (dev impl)

`Infrastructure/Services/LocalFileStorage.cs`:

- Writes blobs under a configured root directory; **keys are fresh GUIDs**, deliberately *not* tied to
  the DB id — this enables the upload-before-persist + compensating-delete ordering #2 needs.
- `SaveAsync`: generate GUID key, stream content to `{root}/{key}`, return the key.
- `OpenAsync`: return a read `FileStream` for `{root}/{key}`, or `null` if the file does not exist.
- `DeleteAsync`: delete `{root}/{key}` if present; no-op otherwise (idempotent).
- Registered as a **singleton** (stateless aside from the root path).

### DI wiring

`Infrastructure/Services/FileStorageDependencyInjection.cs` — `AddFileStorage(this IServiceCollection,
IConfiguration)`, mirroring `AddQuantityExtraction`:

- Reads `FileStorage:LocalPath` from config; binds `IFileStorage → LocalFileStorage`.
- Called from the host composition in `Program.cs` alongside the other `AddXxx` extensions.
- v1 always binds `LocalFileStorage`; sub-feature #4 introduces a `FileStorage:Provider` switch behind
  the same port. The `LocalPath` is supplied in the dev-up / `LocalDb` launch path.

## Migration

One EF migration adds `Type` (int, default `0`) + the five nullable columns to `ListItems`, with
`HasMaxLength` on the new string columns set from the constants above (fresh columns — widths are set
here, not retrofitted). No new table, no inheritance mapping. Default backfills existing rows to
`Text`. Applied automatically at startup via `MigrateAsync()`.

`ListItemConfiguration` gains the new property mappings: `Type` (required, `HasDefaultValue`), and the
five nullable columns with their max lengths.

## Testing (`Frigorino.Test`)

Aggregate tests are pure (no DB); the storage test uses a temp directory.

- **`AddMediaItem` happy paths:** valid `Image` (with thumbnail) and valid `Document` (no thumbnail)
  — assert `Type`, `Text == ""`, `Comment == caption`, file columns set, placed in the unchecked
  section.
- **`AddMediaItem` rejections:** `Text` type; content-type outside the per-type allowlist; size `0`
  and over cap; missing `StorageKey`; missing `OriginalFileName`; over-length filename/key; `Image`
  without thumbnail; `Document` with thumbnail; caption over `CommentMaxLength`.
- **Lifecycle uniformity:** a media item toggles status, reorders, compacts, soft-deletes (assert the
  file columns are **retained** when `IsActive` flips false), and restores — same paths as text items.
- **`LocalFileStorage` round-trip:** `Save` → `Open` → bytes match → `Delete` → `Open` returns `null`.

ArchUnitNET layer rules already cover the placement: `IFileStorage`/`StoredFile`/`ListItemType` in
`Domain`, `LocalFileStorage` in `Infrastructure`. No new rule needed.

## Dockerfile

No new project and no new NuGet package in #1 (ImageSharp arrives in #2). Dockerfile expected
unchanged; confirm with a `docker build` only if drift is suspected.
