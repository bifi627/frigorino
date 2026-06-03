# Rich list items #1 — Typed-item foundation + storage seam — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ListItem` a typed item (`Text`/`Image`/`Document`) on the existing flat table, add a vendor-neutral `IFileStorage` port with a `LocalFileStorage` dev backend, and a `List.AddMediaItem` aggregate method — all backend-only, no HTTP endpoints.

**Architecture:** One EF migration adds an enum column + five nullable file columns (no inheritance). A `StoredFile` value object carries file metadata into the pure `List.AddMediaItem` method, which validates per-type allowlists/limits and a thumbnail invariant. `IFileStorage` is a lean bytes-only port (content-type/length live in DB columns); `LocalFileStorage` writes GUID-keyed blobs under a configured directory, wired via a config-driven DI extension that mirrors `AddQuantityExtraction`.

**Tech Stack:** .NET 10, EF Core (Postgres), FluentResults, xUnit + EF InMemory. No new NuGet package.

**Spec:** [`docs/superpowers/specs/2026-06-03-rich-list-items-1-foundation-design.md`](../specs/2026-06-03-rich-list-items-1-foundation-design.md)

---

## File Structure

**Create:**
- `Application/Frigorino.Domain/Entities/ListItemType.cs` — the type enum.
- `Application/Frigorino.Domain/Files/StoredFile.cs` — file-metadata value object.
- `Application/Frigorino.Domain/Interfaces/IFileStorage.cs` — storage port.
- `Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs` — dev impl.
- `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs` — DI extension.
- `Application/Frigorino.Test/Domain/ListAggregateMediaItemTests.cs` — `AddMediaItem` + lifecycle tests.
- `Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs` — round-trip test.
- `Application/Frigorino.Test/Infrastructure/ListItemMediaPersistenceTests.cs` — column round-trip.
- EF migration (generated) under `Application/Frigorino.Infrastructure/Migrations/`.

**Modify:**
- `Application/Frigorino.Domain/Entities/ListItem.cs` — new columns + constants.
- `Application/Frigorino.Domain/Entities/List.cs` — `AddMediaItem`.
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs` — new mappings.
- `Application/Frigorino.Web/Program.cs` — call `AddFileStorage`.
- `Application/Frigorino.Web/appsettings.json` — empty `FileStorage:LocalPath` placeholder.

---

## Task 1: Domain scaffolding — enum, value object, port, columns, constants

**Files:**
- Create: `Application/Frigorino.Domain/Entities/ListItemType.cs`
- Create: `Application/Frigorino.Domain/Files/StoredFile.cs`
- Create: `Application/Frigorino.Domain/Interfaces/IFileStorage.cs`
- Modify: `Application/Frigorino.Domain/Entities/ListItem.cs`

- [ ] **Step 1: Create the `ListItemType` enum**

Create `Application/Frigorino.Domain/Entities/ListItemType.cs`:

```csharp
namespace Frigorino.Domain.Entities
{
    // Stored as int in Postgres (EF default); serialized as its string name on the wire via the
    // global JsonStringEnumConverter. Existing rows backfill to Text (the migration default).
    public enum ListItemType
    {
        Text = 0,
        Image = 1,
        Document = 2,
    }
}
```

- [ ] **Step 2: Create the `StoredFile` value object**

Create `Application/Frigorino.Domain/Files/StoredFile.cs`:

```csharp
namespace Frigorino.Domain.Files
{
    // The metadata the storage pipeline produces for one stored blob. Travels as one unit into
    // List.AddMediaItem (mirrors the Quantity VO passed to AddItem). ThumbnailKey is set only for
    // images; null for documents.
    public sealed record StoredFile(
        string StorageKey,
        string? ThumbnailKey,
        string ContentType,
        string OriginalFileName,
        long SizeBytes);
}
```

- [ ] **Step 3: Create the `IFileStorage` port**

Create `Application/Frigorino.Domain/Interfaces/IFileStorage.cs`:

```csharp
namespace Frigorino.Domain.Interfaces
{
    // Vendor-neutral blob store. Lean by design: it only moves bytes. Content-type and length live
    // in the ListItem columns, so the store never has to persist or echo metadata — this keeps it
    // truly backend-agnostic. Keys are opaque (GUID-based) and not tied to the DB id, which lets the
    // upload happen before the row is persisted (with a compensating Delete on failure — sub-feature #2).
    public interface IFileStorage
    {
        Task<string> SaveAsync(Stream content, CancellationToken ct); // returns opaque key
        Task<Stream?> OpenAsync(string key, CancellationToken ct);     // null if the key is absent
        Task DeleteAsync(string key, CancellationToken ct);            // idempotent (no-op if absent)
    }
}
```

- [ ] **Step 4: Add the new columns and constants to `ListItem`**

In `Application/Frigorino.Domain/Entities/ListItem.cs`, add the media constants after the existing
`CommentMaxLength` constant (line 10):

```csharp
        // Media-item limits. Source of truth for both AddMediaItem validation and the EF
        // configuration (fresh columns — widths are set here, not retrofitted). Tunable.
        public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB
        public const int OriginalFileNameMaxLength = 255;
        public const int ContentTypeMaxLength = 255;
        public const int StorageKeyMaxLength = 200;

        // Allowlists are arrays (can't be const). HEIC / office formats are deliberately excluded
        // from v1; extend here when sub-feature #2/#3 widens support.
        public static readonly string[] ImageContentTypes =
            { "image/jpeg", "image/png", "image/webp" };
        public static readonly string[] DocumentContentTypes =
            { "application/pdf" };
```

Then add the typed-item fields. Add `Type` right after the `Id`/`ListId` block (after line 13's
`Text`) and the file columns after the `Comment` block (after line 19). Insert:

```csharp
        // Text (default) | Image | Document. Existing rows backfill to Text via the migration default.
        public ListItemType Type { get; set; } = ListItemType.Text;
```

and, after the `Comment` property:

```csharp
        // Media-item columns. All null for Text items. For media items Text == "" and the optional
        // caption reuses Comment (clean-separation: Text/Comment keep one meaning each).
        public string? StorageKey { get; set; }
        public string? ThumbnailStorageKey { get; set; } // images only
        public string? OriginalFileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSizeBytes { get; set; }
```

- [ ] **Step 5: Build to verify the domain project compiles**

Run: `dotnet build Application/Frigorino.Domain`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/ListItemType.cs \
        Application/Frigorino.Domain/Files/StoredFile.cs \
        Application/Frigorino.Domain/Interfaces/IFileStorage.cs \
        Application/Frigorino.Domain/Entities/ListItem.cs
git commit -m "feat(domain): add typed-item enum, StoredFile VO, IFileStorage port, media columns"
```

---

## Task 2: `List.AddMediaItem` aggregate method (TDD)

**Files:**
- Test: `Application/Frigorino.Test/Domain/ListAggregateMediaItemTests.cs`
- Modify: `Application/Frigorino.Domain/Entities/List.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/ListAggregateMediaItemTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for List.AddMediaItem and the uniform behavior of media items through the
    // existing item lifecycle methods. No DbContext.
    public class ListAggregateMediaItemTests
    {
        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        private static List NewList()
        {
            return new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = HouseholdId,
                CreatedByUserId = CreatorId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true,
            };
        }

        private static StoredFile ImageFile() =>
            new("images/key-1", "images/thumb-1", "image/jpeg", "fridge.jpg", 1024);

        private static StoredFile DocumentFile() =>
            new("docs/key-1", null, "application/pdf", "warranty.pdf", 2048);

        // ------- happy paths -------

        [Fact]
        public void AddMediaItem_ValidImage_SetsColumnsAndPlacesInUnchecked()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Image, "front of fridge", ImageFile());

            Assert.True(result.IsSuccess);
            var item = result.Value;
            Assert.Equal(ListItemType.Image, item.Type);
            Assert.Equal("", item.Text);
            Assert.Equal("front of fridge", item.Comment);
            Assert.Equal("images/key-1", item.StorageKey);
            Assert.Equal("images/thumb-1", item.ThumbnailStorageKey);
            Assert.Equal("image/jpeg", item.ContentType);
            Assert.Equal("fridge.jpg", item.OriginalFileName);
            Assert.Equal(1024, item.FileSizeBytes);
            Assert.False(item.Status);
            Assert.True(item.IsActive);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item.SortOrder);
        }

        [Fact]
        public void AddMediaItem_ValidDocument_HasNoThumbnail()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Document, null, DocumentFile());

            Assert.True(result.IsSuccess);
            Assert.Equal(ListItemType.Document, result.Value.Type);
            Assert.Null(result.Value.ThumbnailStorageKey);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddMediaItem_TrimsCaptionIntoComment()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Image, "  hello  ", ImageFile());

            Assert.True(result.IsSuccess);
            Assert.Equal("hello", result.Value.Comment);
        }

        // ------- rejections -------

        [Fact]
        public void AddMediaItem_TextType_Fails()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Text, null, ImageFile());

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Type), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_DisallowedContentTypeForImage_FailsKeyedOnContentType()
        {
            var list = NewList();
            var badType = new StoredFile("k", "t", "application/pdf", "x.pdf", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, badType);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ContentType), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_DisallowedContentTypeForDocument_FailsKeyedOnContentType()
        {
            var list = NewList();
            var badType = new StoredFile("k", null, "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Document, null, badType);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ContentType), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_ZeroSize_FailsKeyedOnFileSize()
        {
            var list = NewList();
            var zero = new StoredFile("k", "t", "image/jpeg", "x.jpg", 0);

            var result = list.AddMediaItem(ListItemType.Image, null, zero);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.FileSizeBytes), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_OverSizeCap_FailsKeyedOnFileSize()
        {
            var list = NewList();
            var tooBig = new StoredFile("k", "t", "image/jpeg", "x.jpg", ListItem.MaxFileSizeBytes + 1);

            var result = list.AddMediaItem(ListItemType.Image, null, tooBig);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.FileSizeBytes), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_MissingStorageKey_FailsKeyedOnStorageKey()
        {
            var list = NewList();
            var noKey = new StoredFile("  ", "t", "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, noKey);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.StorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_MissingFileName_FailsKeyedOnFileName()
        {
            var list = NewList();
            var noName = new StoredFile("k", "t", "image/jpeg", "   ", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, noName);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.OriginalFileName), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_ImageWithoutThumbnail_FailsKeyedOnThumbnail()
        {
            var list = NewList();
            var noThumb = new StoredFile("k", null, "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, noThumb);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ThumbnailStorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_DocumentWithThumbnail_FailsKeyedOnThumbnail()
        {
            var list = NewList();
            var withThumb = new StoredFile("k", "t", "application/pdf", "x.pdf", 10);

            var result = list.AddMediaItem(ListItemType.Document, null, withThumb);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ThumbnailStorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_CaptionTooLong_FailsKeyedOnComment()
        {
            var list = NewList();
            var caption = new string('x', ListItem.CommentMaxLength + 1);

            var result = list.AddMediaItem(ListItemType.Image, caption, ImageFile());

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Comment), result.Errors[0].Metadata["Property"]);
        }

        // ------- lifecycle uniformity -------

        [Fact]
        public void MediaItem_TogglesReordersCompacts_LikeTextItems()
        {
            var list = NewList();
            var media = list.AddMediaItem(ListItemType.Image, null, ImageFile()).Value;
            list.AddItem("Milk");

            Assert.True(list.ToggleItemStatus(media.Id).IsSuccess);
            Assert.True(media.Status);
            Assert.True(list.CompactItems().IsSuccess);
            Assert.True(list.ReorderItem(media.Id, 0).IsSuccess);
        }

        [Fact]
        public void MediaItem_SoftDeleteRetainsFileColumns_AndRestores()
        {
            var list = NewList();
            var media = list.AddMediaItem(ListItemType.Image, "cap", ImageFile()).Value;

            Assert.True(list.RemoveItem(media.Id).IsSuccess);
            Assert.False(media.IsActive);
            // Blob metadata must survive soft-delete so restore re-exposes the same file.
            Assert.Equal("images/key-1", media.StorageKey);
            Assert.Equal("images/thumb-1", media.ThumbnailStorageKey);

            var restored = list.RestoreItem(media.Id);
            Assert.True(restored.IsSuccess);
            Assert.True(media.IsActive);
            Assert.Equal("images/key-1", media.StorageKey);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateMediaItemTests"`
Expected: FAIL — compile error, `List` does not contain a definition for `AddMediaItem`.

- [ ] **Step 3: Implement `AddMediaItem`**

In `Application/Frigorino.Domain/Entities/List.cs`, add `using Frigorino.Domain.Files;` at the top
(after `using Frigorino.Domain.Quantities;`). Then add the method immediately after `AddItem`
(after line 163):

```csharp
        public Result<ListItem> AddMediaItem(ListItemType type, string? caption, StoredFile file)
        {
            var errors = new System.Collections.Generic.List<IError>();

            if (type != ListItemType.Image && type != ListItemType.Document)
            {
                errors.Add(new Error("Media item type must be Image or Document.")
                    .WithMetadata("Property", nameof(ListItem.Type)));
                // Bail early: the allowlist branch below dereferences `type`.
                return Result.Fail<ListItem>(errors);
            }

            var allowed = type == ListItemType.Image
                ? ListItem.ImageContentTypes
                : ListItem.DocumentContentTypes;
            if (string.IsNullOrWhiteSpace(file.ContentType) || !allowed.Contains(file.ContentType))
            {
                errors.Add(new Error($"Content type '{file.ContentType}' is not allowed for {type} items.")
                    .WithMetadata("Property", nameof(ListItem.ContentType)));
            }

            if (string.IsNullOrWhiteSpace(file.StorageKey) || file.StorageKey.Length > ListItem.StorageKeyMaxLength)
            {
                errors.Add(new Error("Storage key is required.")
                    .WithMetadata("Property", nameof(ListItem.StorageKey)));
            }

            if (string.IsNullOrWhiteSpace(file.OriginalFileName)
                || file.OriginalFileName.Length > ListItem.OriginalFileNameMaxLength)
            {
                errors.Add(new Error($"File name is required and must be {ListItem.OriginalFileNameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(ListItem.OriginalFileName)));
            }

            if (file.SizeBytes <= 0 || file.SizeBytes > ListItem.MaxFileSizeBytes)
            {
                errors.Add(new Error($"File size must be between 1 and {ListItem.MaxFileSizeBytes} bytes.")
                    .WithMetadata("Property", nameof(ListItem.FileSizeBytes)));
            }

            // Type/thumbnail invariant: images carry a thumbnail, documents must not.
            var hasThumbnail = !string.IsNullOrWhiteSpace(file.ThumbnailKey);
            if (type == ListItemType.Image && !hasThumbnail)
            {
                errors.Add(new Error("Image items require a thumbnail key.")
                    .WithMetadata("Property", nameof(ListItem.ThumbnailStorageKey)));
            }
            else if (type == ListItemType.Document && hasThumbnail)
            {
                errors.Add(new Error("Document items must not have a thumbnail key.")
                    .WithMetadata("Property", nameof(ListItem.ThumbnailStorageKey)));
            }

            errors.AddRange(ValidateComment(caption));

            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new ListItem
            {
                ListId = Id,
                Type = type,
                Text = "",
                Comment = NormalizeComment(caption),
                StorageKey = file.StorageKey,
                ThumbnailStorageKey = file.ThumbnailKey,
                ContentType = file.ContentType,
                OriginalFileName = file.OriginalFileName,
                FileSizeBytes = file.SizeBytes,
                Status = false,
                SortOrder = ComputeAppendSortOrder(targetStatus: false),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            ListItems.Add(item);
            return Result.Ok(item);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateMediaItemTests"`
Expected: PASS — all tests green.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs \
        Application/Frigorino.Test/Domain/ListAggregateMediaItemTests.cs
git commit -m "feat(domain): add List.AddMediaItem with per-type validation"
```

---

## Task 3: EF configuration + migration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ListItemMediaPersistenceTests.cs`
- Create (generated): migration under `Application/Frigorino.Infrastructure/Migrations/`

- [ ] **Step 1: Write the failing persistence test**

Create `Application/Frigorino.Test/Infrastructure/ListItemMediaPersistenceTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class ListItemMediaPersistenceTests
    {
        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task ListItem_RoundTripsMediaColumns()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "",
                    Type = ListItemType.Image,
                    StorageKey = "images/key-1",
                    ThumbnailStorageKey = "images/thumb-1",
                    OriginalFileName = "fridge.jpg",
                    ContentType = "image/jpeg",
                    FileSizeBytes = 1024,
                });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Image, item.Type);
            Assert.Equal("images/key-1", item.StorageKey);
            Assert.Equal("images/thumb-1", item.ThumbnailStorageKey);
            Assert.Equal("fridge.jpg", item.OriginalFileName);
            Assert.Equal("image/jpeg", item.ContentType);
            Assert.Equal(1024, item.FileSizeBytes);
        }

        [Fact]
        public async Task ListItem_DefaultsToTextType()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem { ListId = 1, Text = "milk" });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Text, item.Type);
            Assert.Null(item.StorageKey);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListItemMediaPersistenceTests"`
Expected: FAIL — `ListItem` has no `Type`/`StorageKey` (it will fail to compile until Task 1 is built; if Task 1 is already merged it compiles but the test still validates the mappings added next).

> Note: the InMemory provider does not enforce column lengths, so these tests pass once the
> properties exist. The mapping/`HasMaxLength`/default config in Step 3 is what makes the **Postgres**
> migration correct; verify the generated migration in Step 5.

- [ ] **Step 3: Add the EF mappings**

In `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`,
add inside `Configure(...)`, after the `Comment` mapping (line 24):

```csharp
            builder.Property(li => li.Type)
                .IsRequired()
                .HasDefaultValue(ListItemType.Text);

            builder.Property(li => li.StorageKey)
                .HasMaxLength(ListItem.StorageKeyMaxLength);

            builder.Property(li => li.ThumbnailStorageKey)
                .HasMaxLength(ListItem.StorageKeyMaxLength);

            builder.Property(li => li.OriginalFileName)
                .HasMaxLength(ListItem.OriginalFileNameMaxLength);

            builder.Property(li => li.ContentType)
                .HasMaxLength(ListItem.ContentTypeMaxLength);

            builder.Property(li => li.FileSizeBytes);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListItemMediaPersistenceTests"`
Expected: PASS.

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddListItemMediaColumns \
  --project Application/Frigorino.Infrastructure \
  --startup-project Application/Frigorino.Web
```
Expected: a new migration under `Application/Frigorino.Infrastructure/Migrations/`. Open it and
confirm: it `AddColumn`s `Type` (int, `defaultValue: 0`), `StorageKey` (maxLength 200, nullable),
`ThumbnailStorageKey` (maxLength 200, nullable), `OriginalFileName` (maxLength 255, nullable),
`ContentType` (maxLength 255, nullable), `FileSizeBytes` (bigint, nullable). It must contain **no
other table changes**. If unrelated changes appear, delete the migration, fix the config, and
regenerate.

- [ ] **Step 6: Build to verify the migration compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs \
        Application/Frigorino.Infrastructure/Migrations/ \
        Application/Frigorino.Test/Infrastructure/ListItemMediaPersistenceTests.cs
git commit -m "feat(infra): map media columns + AddListItemMediaColumns migration"
```

---

## Task 4: `LocalFileStorage` dev implementation (TDD)

**Files:**
- Test: `Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs`
- Create: `Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs`

- [ ] **Step 1: Write the failing round-trip test**

Create `Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs`:

```csharp
using System.Text;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class LocalFileStorageTests : IDisposable
    {
        private readonly string _root =
            Path.Combine(Path.GetTempPath(), "frigorino-test-" + Guid.NewGuid().ToString("N"));

        private LocalFileStorage NewStorage() => new(_root);

        [Fact]
        public async Task SaveOpenDelete_RoundTrips()
        {
            var storage = NewStorage();
            var bytes = Encoding.UTF8.GetBytes("hello blob");

            string key;
            using (var input = new MemoryStream(bytes))
            {
                key = await storage.SaveAsync(input, CancellationToken.None);
            }
            Assert.False(string.IsNullOrWhiteSpace(key));

            using (var opened = await storage.OpenAsync(key, CancellationToken.None))
            {
                Assert.NotNull(opened);
                using var ms = new MemoryStream();
                await opened!.CopyToAsync(ms);
                Assert.Equal(bytes, ms.ToArray());
            }

            await storage.DeleteAsync(key, CancellationToken.None);
            var afterDelete = await storage.OpenAsync(key, CancellationToken.None);
            Assert.Null(afterDelete);
        }

        [Fact]
        public async Task OpenAsync_UnknownKey_ReturnsNull()
        {
            var storage = NewStorage();
            var result = await storage.OpenAsync("does-not-exist", CancellationToken.None);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAsync_UnknownKey_DoesNotThrow()
        {
            var storage = NewStorage();
            await storage.DeleteAsync("does-not-exist", CancellationToken.None);
        }

        [Fact]
        public async Task SaveAsync_GeneratesDistinctKeys()
        {
            var storage = NewStorage();
            using var a = new MemoryStream(new byte[] { 1 });
            using var b = new MemoryStream(new byte[] { 2 });

            var keyA = await storage.SaveAsync(a, CancellationToken.None);
            var keyB = await storage.SaveAsync(b, CancellationToken.None);

            Assert.NotEqual(keyA, keyB);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~LocalFileStorageTests"`
Expected: FAIL — `LocalFileStorage` does not exist.

- [ ] **Step 3: Implement `LocalFileStorage`**

Create `Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs`:

```csharp
using Frigorino.Domain.Interfaces;

namespace Frigorino.Infrastructure.Services
{
    // Dev/test blob backend: writes each blob to {root}/{guid}. Keys are GUIDs, deliberately not
    // tied to the DB id, so an upload can happen before the row is persisted (sub-feature #2 adds a
    // compensating Delete on persist failure). Stateless apart from the root path → singleton.
    public sealed class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(string root)
        {
            _root = root;
            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveAsync(Stream content, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N");
            var path = PathFor(key);
            await using var file = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, ct);
            return key;
        }

        public Task<Stream?> OpenAsync(string key, CancellationToken ct)
        {
            var path = PathFor(key);
            if (!File.Exists(path))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            var path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return Task.CompletedTask;
        }

        // Guard against path traversal: only the bare key (no separators) is ever a valid file.
        private string PathFor(string key)
        {
            var name = Path.GetFileName(key);
            return Path.Combine(_root, name);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~LocalFileStorageTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs \
        Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs
git commit -m "feat(infra): add LocalFileStorage dev backend for IFileStorage"
```

---

## Task 5: DI wiring + configuration

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/Program.cs`
- Modify: `Application/Frigorino.Web/appsettings.json`

- [ ] **Step 1: Create the DI extension**

Create `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class FileStorageDependencyInjection
    {
        // v1 always binds LocalFileStorage. Sub-feature #4 introduces a FileStorage:Provider switch
        // behind the same IFileStorage port. FileStorage:LocalPath defaults to a "blobs" directory
        // under the content root when unset (fine for dev/test; production sets it explicitly).
        public static IServiceCollection AddFileStorage(
            this IServiceCollection services, IConfiguration configuration)
        {
            var root = configuration["FileStorage:LocalPath"];
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(AppContext.BaseDirectory, "blobs");
            }

            services.AddSingleton<IFileStorage>(new LocalFileStorage(root));
            return services;
        }
    }
}
```

- [ ] **Step 2: Wire it into `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, add after the `AddBackgroundTaskQueue()` line (line 82):

```csharp
builder.Services.AddFileStorage(builder.Configuration);
```

Confirm `Frigorino.Infrastructure.Services` is in scope (the other `AddXxx` extensions live in that
namespace and are already used in this file, so no new `using` is needed).

- [ ] **Step 3: Add the config placeholder**

In `Application/Frigorino.Web/appsettings.json`, add a top-level `FileStorage` section (empty
placeholder, like the other secrets):

```json
  "FileStorage": {
    "LocalPath": ""
  }
```

- [ ] **Step 4: Build to verify wiring compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs \
        Application/Frigorino.Web/Program.cs \
        Application/Frigorino.Web/appsettings.json
git commit -m "feat(infra): wire AddFileStorage (LocalFileStorage) into the host"
```

---

## Task 6: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS — all unit + integration tests green, including the new media/storage tests and the
ArchUnitNET layer rules (which confirm `IFileStorage`/`StoredFile`/`ListItemType` stay in `Domain`
and `LocalFileStorage` in `Infrastructure`).

> If the integration tests need Docker (Testcontainers) and the daemon is unreachable, ask the user
> to start Docker Desktop rather than skipping.

- [ ] **Step 2: Confirm no Dockerfile drift**

No new project and no new NuGet package were added, so the Dockerfile should be unchanged. Only if a
project reference changed, run `docker build -f Application/Dockerfile -t frigorino .` to confirm.

- [ ] **Step 3: Final review**

Confirm the branch contains exactly: the enum, `StoredFile`, `IFileStorage`, `ListItem` columns +
constants, `List.AddMediaItem`, the EF config + one migration, `LocalFileStorage`, the DI extension +
`Program.cs`/`appsettings.json` wiring, and the three test files. No HTTP slice, no ImageSharp, no
frontend (those are sub-feature #2).

---

## Self-Review Notes

- **Spec coverage:** enum ✓ (T1), 5 nullable columns ✓ (T1/T3), constants + allowlists ✓ (T1),
  caption→`Comment` ✓ (T2 tests assert it), `StoredFile` VO ✓ (T1), `AddMediaItem` + all validations
  incl. thumbnail invariant ✓ (T2), lean `IFileStorage` ✓ (T1), `LocalFileStorage` ✓ (T4), GUID keys
  ✓ (T4), DI extension + config ✓ (T5), one migration ✓ (T3), domain + round-trip + storage tests ✓
  (T2/T3/T4), Dockerfile unchanged ✓ (T6).
- **Type consistency:** `AddMediaItem(ListItemType, string?, StoredFile)`, `StoredFile(StorageKey,
  ThumbnailKey, ContentType, OriginalFileName, SizeBytes)`, and `IFileStorage` 3-method signature are
  used identically across tasks and tests.
- **No placeholders:** every code/command step is complete.
