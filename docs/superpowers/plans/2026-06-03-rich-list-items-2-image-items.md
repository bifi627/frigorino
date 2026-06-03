# Rich list items #2 — Image items, end-to-end — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Take an image list item all the way through the stack — attach → upload → server-side re-encode + thumbnail → authenticated byte serving → list render → full-res lightbox.

**Architecture:** A small `IImageProcessor` port (ImageSharp impl in Infrastructure) re-encodes + strips metadata for a full-res + thumbnail rendition. Three new vertical slices (`CreateMediaItem` multipart, `GetItemFile`, `GetItemThumbnail`) ride the existing `IFileStorage` seam and `List.AddMediaItem` aggregate method from sub-feature #1. The React SPA gets a renderer switch, a composer attach action-feature, a list-owned preview sheet, a generated multipart upload hook, and an auth-aware blob hook.

**Tech Stack:** .NET 10 minimal-API vertical slices, FluentResults, EF Core (Postgres / InMemory for tests), SixLabors.ImageSharp, xUnit + FakeItEasy, React 19 + MUI + TanStack Query, hey-api generated client, Reqnroll + Playwright + Postgres Testcontainers.

**Spec:** `docs/superpowers/specs/2026-06-03-rich-list-items-2-image-items-design.md`

**Branch:** `feat/rich-list-items-2-image-items` (already checked out, off `feat/rich-list-items`).

**Context already in place from sub-feature #1 (do not re-create):**
- `Frigorino.Domain/Entities/ListItemType.cs` — `enum ListItemType { Text=0, Image=1, Document=2 }`.
- `Frigorino.Domain/Files/StoredFile.cs` — `record StoredFile(string StorageKey, string? ThumbnailKey, string ContentType, string OriginalFileName, long SizeBytes)`.
- `Frigorino.Domain/Interfaces/IFileStorage.cs` — `SaveAsync(Stream,ct)→key`, `OpenAsync(key,ct)→Stream?`, `DeleteAsync(key,ct)`.
- `Frigorino.Domain/Entities/List.cs` — `AddMediaItem(ListItemType type, string? caption, StoredFile file)` (validates allowlist/size/thumbnail-invariant; sets `Text=""`, caption→`Comment`).
- `Frigorino.Domain/Entities/ListItem.cs` — media columns + constants `MaxFileSizeBytes` (25 MB, `long`), `ImageContentTypes = ["image/jpeg","image/png","image/webp"]`, `OriginalFileNameMaxLength=255`, `StorageKeyMaxLength=200`.
- `Frigorino.Infrastructure/Services/LocalFileStorage.cs` + `FileStorageDependencyInjection.cs` (`AddFileStorage`, already wired in `Program.cs`).

**Conventions to follow (verified against the codebase):**
- Slice handlers are `public static Task<Results<...>> Handle(...)` so they are unit-testable directly (see `GetMyInventoryNotificationEndpoint.Handle`). Registration is a `public static IEndpointRouteBuilder MapXxx(this IEndpointRouteBuilder app)` extension.
- Domain validation errors carry `.WithMetadata("Property", nameof(...))`; the slice calls `result.ToValidationProblem()` (`Frigorino.Features/Results/ResultExtensions.cs`).
- Membership gate: `await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct)` → `TypedResults.NotFound()` when null.
- C# brace style: always block `{ }` even for single-line bodies.
- Tests: InMemory DB via `TestApplicationDbContext`, `A.Fake<...>()` for services, no translated-text assertions.

---

## Task 1: `IImageProcessor` port + ImageSharp implementation

**Files:**
- Create: `Application/Frigorino.Domain/Interfaces/IImageProcessor.cs`
- Modify: `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj` (add ImageSharp PackageReference)
- Create: `Application/Frigorino.Infrastructure/Services/ImageSharpImageProcessor.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ImageSharpImageProcessorTests.cs`

- [ ] **Step 1: Add the ImageSharp NuGet package (exact-pinned)**

Run:
```bash
cd C:/Repositories/frigorino && dotnet add Application/Frigorino.Infrastructure package SixLabors.ImageSharp
```
Then open `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj` and confirm the
reference uses the **exact** resolved version (no `*`/range — project pinning policy), e.g.:
```xml
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
```
(Use whatever version `dotnet add` resolved; just make it an exact version string.)

- [ ] **Step 2: Define the port + result record**

Create `Application/Frigorino.Domain/Interfaces/IImageProcessor.cs`:
```csharp
using FluentResults;

namespace Frigorino.Domain.Interfaces
{
    // Re-encodes an uploaded image into two sanitized renditions. Kept deliberately small (one
    // method) so the library (ImageSharp) is swappable behind it — mirrors the IItemClassifier /
    // IQuantityExtractor / IFileStorage seams. The slice depends on this abstraction and is
    // unit-tested with a fake; the ImageSharp impl lives in Infrastructure.
    public interface IImageProcessor
    {
        // Decodes (validating the bytes are a real, allowed image), auto-orients from EXIF, strips
        // ALL metadata, and re-encodes a full-res + thumbnail rendition. Returns Fail when the input
        // is not a decodable JPEG/PNG/WebP (slice maps that to 400).
        Task<Result<ProcessedImage>> ProcessAsync(Stream input, CancellationToken ct);
    }

    // Bytes + the single content-type we actually wrote (both renditions share it). FullResSizeBytes
    // is the stored full-res length, recorded on the ListItem.
    public sealed record ProcessedImage(
        byte[] FullRes,
        byte[] Thumbnail,
        string ContentType,
        long FullResSizeBytes);
}
```

- [ ] **Step 3: Write the failing test**

Create `Application/Frigorino.Test/Infrastructure/ImageSharpImageProcessorTests.cs`:
```csharp
using FluentResults;
using Frigorino.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Frigorino.Test.Infrastructure
{
    public class ImageSharpImageProcessorTests
    {
        private static byte[] MakePng(int width, int height)
        {
            using var image = new Image<Rgba32>(width, height);
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            return ms.ToArray();
        }

        [Fact]
        public async Task ProcessAsync_ValidPng_ReturnsWebpRenditions()
        {
            var processor = new ImageSharpImageProcessor();
            using var input = new MemoryStream(MakePng(1200, 900));

            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("image/webp", result.Value.ContentType);
            Assert.NotEmpty(result.Value.FullRes);
            Assert.NotEmpty(result.Value.Thumbnail);
            Assert.Equal(result.Value.FullRes.Length, (int)result.Value.FullResSizeBytes);

            // Both renditions are real WebP images.
            using var full = Image.Load(result.Value.FullRes);
            using var thumb = Image.Load(result.Value.Thumbnail);
            Assert.Equal("WEBP", Image.DetectFormat(result.Value.FullRes).Name);
            Assert.True(Math.Max(thumb.Width, thumb.Height) <= 480);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotUpscaleSmallImage()
        {
            var processor = new ImageSharpImageProcessor();
            using var input = new MemoryStream(MakePng(100, 80));

            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using var full = Image.Load(result.Value.FullRes);
            Assert.Equal(100, full.Width);
            Assert.Equal(80, full.Height);
        }

        [Fact]
        public async Task ProcessAsync_GarbageBytes_ReturnsFail()
        {
            var processor = new ImageSharpImageProcessor();
            using var input = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task ProcessAsync_StripsExifMetadata()
        {
            var processor = new ImageSharpImageProcessor();
            byte[] withExif;
            using (var image = new Image<Rgba32>(50, 50))
            {
                image.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
                image.Metadata.ExifProfile.SetValue(
                    SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Copyright, "secret");
                using var ms = new MemoryStream();
                image.Save(ms, new PngEncoder());
                withExif = ms.ToArray();
            }

            using var input = new MemoryStream(withExif);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using var full = Image.Load(result.Value.FullRes);
            Assert.Null(full.Metadata.ExifProfile);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ImageSharpImageProcessorTests"`
Expected: FAIL — `ImageSharpImageProcessor` does not exist (compile error).

- [ ] **Step 5: Implement the processor**

Create `Application/Frigorino.Infrastructure/Services/ImageSharpImageProcessor.cs`:
```csharp
using FluentResults;
using Frigorino.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Frigorino.Infrastructure.Services
{
    // ImageSharp-backed IImageProcessor. Encoding policy (sizes/quality/format) lives here as
    // Infrastructure constants — it is rendering policy, not an aggregate invariant. Stateless →
    // safe as a singleton.
    public sealed class ImageSharpImageProcessor : IImageProcessor
    {
        private const int FullResMaxEdge = 2560;
        private const int ThumbnailMaxEdge = 480;
        private const int FullResQuality = 82;
        private const int ThumbnailQuality = 75;
        private const string WebpContentType = "image/webp";

        // Only these decoders are accepted — shrinks the decode attack surface and avoids surprises
        // (e.g. animated GIF). The detected format name is compared case-insensitively.
        private static readonly HashSet<string> AllowedInputFormats =
            new(StringComparer.OrdinalIgnoreCase) { "JPEG", "PNG", "WEBP" };

        public async Task<Result<ProcessedImage>> ProcessAsync(Stream input, CancellationToken ct)
        {
            // Buffer so we can both detect the format and decode from the start.
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            try
            {
                var format = await Image.DetectFormatAsync(buffer, ct);
                if (format is null || !AllowedInputFormats.Contains(format.Name))
                {
                    return Result.Fail(new Error("Unsupported image format.")
                        .WithMetadata("Property", "file"));
                }

                buffer.Position = 0;
                using var image = await Image.LoadAsync(buffer, ct);

                // AutoOrient bakes EXIF rotation into pixels (so stripping EXIF can't desync it).
                image.Mutate(x => x.AutoOrient());
                StripMetadata(image);

                var fullRes = await EncodeAsync(image, FullResMaxEdge, FullResQuality, ct);
                var thumbnail = await EncodeAsync(image, ThumbnailMaxEdge, ThumbnailQuality, ct);

                return Result.Ok(new ProcessedImage(
                    fullRes, thumbnail, WebpContentType, fullRes.LongLength));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result.Fail(new Error("Could not decode the uploaded image.")
                    .WithMetadata("Property", "file"));
            }
        }

        private static void StripMetadata(Image image)
        {
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;
        }

        // Encode to WebP into a byte[]. Resize (Max, never upscale) only when the image exceeds the
        // bound; otherwise encode the (already auto-oriented, metadata-stripped) image as-is. Neither
        // branch mutates the shared `image`, so it is safe to call twice (full-res then thumbnail).
        private static async Task<byte[]> EncodeAsync(Image image, int maxEdge, int quality, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            if (Math.Max(image.Width, image.Height) > maxEdge)
            {
                using var resized = image.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxEdge, maxEdge),
                    Mode = ResizeMode.Max,
                }));
                await resized.SaveAsync(ms, new WebpEncoder { Quality = quality }, ct);
            }
            else
            {
                await image.SaveAsync(ms, new WebpEncoder { Quality = quality }, ct);
            }
            return ms.ToArray();
        }
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ImageSharpImageProcessorTests"`
Expected: PASS (4 tests).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Domain/Interfaces/IImageProcessor.cs \
        Application/Frigorino.Infrastructure/Services/ImageSharpImageProcessor.cs \
        Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj \
        Application/Frigorino.Infrastructure/packages.lock.json \
        Application/Frigorino.Test/Infrastructure/ImageSharpImageProcessorTests.cs
git commit -m "feat(infra): add IImageProcessor port + ImageSharp impl (webp re-encode + thumbnail)"
```

---

## Task 2: DI wiring for `IImageProcessor`

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/ImageProcessingDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (register after `AddFileStorage`)

- [ ] **Step 1: Add the DI extension**

Create `Application/Frigorino.Infrastructure/Services/ImageProcessingDependencyInjection.cs`:
```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class ImageProcessingDependencyInjection
    {
        // ImageSharp processor is stateless → singleton. Swap the implementation here if the library
        // is ever replaced (the IImageProcessor port keeps callers unchanged).
        public static IServiceCollection AddImageProcessing(this IServiceCollection services)
        {
            services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
            return services;
        }
    }
}
```

- [ ] **Step 2: Register in Program.cs**

In `Application/Frigorino.Web/Program.cs`, find the line `builder.Services.AddFileStorage(builder.Configuration);` and add immediately after it:
```csharp
builder.Services.AddImageProcessing();
```
Ensure `using Frigorino.Infrastructure.Services;` is present (it already is, from `AddFileStorage`).

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/ImageProcessingDependencyInjection.cs \
        Application/Frigorino.Web/Program.cs
git commit -m "feat(infra): wire AddImageProcessing into the host"
```

---

## Task 3: `ListItemResponse` DTO additions

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`
- Test: `Application/Frigorino.Test/Features/ListItemResponseProjectionTests.cs` (create)

The response gains `Type`, `FileName`, `ContentType`, `FileSize`. Caption already flows via the
existing `Comment` field. The new fields are added to the positional ctor **after** the existing
ones, and to **both** `From(...)` and `ToProjection`.

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Features/ListItemResponseProjectionTests.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Features.Lists.Items;

namespace Frigorino.Test.Features
{
    public class ListItemResponseProjectionTests
    {
        [Fact]
        public void From_MediaItem_MapsMediaFields()
        {
            var item = new ListItem
            {
                Id = 5,
                ListId = 2,
                Type = ListItemType.Image,
                Text = "",
                Comment = "the blue one",
                StorageKey = "abc",
                ThumbnailStorageKey = "def",
                OriginalFileName = "photo.jpg",
                ContentType = "image/webp",
                FileSizeBytes = 1234,
                Status = false,
                SortOrder = 1000,
            };

            var dto = ListItemResponse.From(item);

            Assert.Equal(ListItemType.Image, dto.Type);
            Assert.Equal("photo.jpg", dto.FileName);
            Assert.Equal("image/webp", dto.ContentType);
            Assert.Equal(1234, dto.FileSize);
            Assert.Equal("the blue one", dto.Comment);
        }

        [Fact]
        public void From_TextItem_LeavesMediaFieldsNull()
        {
            var item = new ListItem { Id = 1, ListId = 1, Type = ListItemType.Text, Text = "Milk" };

            var dto = ListItemResponse.From(item);

            Assert.Equal(ListItemType.Text, dto.Type);
            Assert.Null(dto.FileName);
            Assert.Null(dto.ContentType);
            Assert.Null(dto.FileSize);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListItemResponseProjectionTests"`
Expected: FAIL — `Type`/`FileName`/`ContentType`/`FileSize` not defined on `ListItemResponse`.

- [ ] **Step 3: Add the fields to the DTO**

Edit `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`. Replace the record header and
both factories so they read:
```csharp
    public sealed record ListItemResponse(
        int Id,
        int ListId,
        string Text,
        string? Comment,
        QuantityDto? Quantity,
        bool Status,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        // Item type — Text (default) | Image | Document. Serialized as its string name.
        ListItemType Type = ListItemType.Text,
        // Media metadata — all null for Text items.
        string? FileName = null,
        string? ContentType = null,
        long? FileSize = null,
        // True only on the create response when the router enqueued LLM extraction for this item
        // (route NeedsExtraction). The client drives its extraction poll off this single signal
        // instead of re-deriving a digit gate; read/projection paths always leave it false.
        bool ExtractionPending = false)
    {
        public static ListItemResponse From(ListItem item)
        {
            return new ListItemResponse(
                item.Id,
                item.ListId,
                item.Text,
                item.Comment,
                item.QuantityValue == null
                    ? null
                    : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                item.Status,
                item.SortOrder,
                item.CreatedAt,
                item.UpdatedAt,
                item.Type,
                item.OriginalFileName,
                item.ContentType,
                item.FileSizeBytes);
        }

        // Promote-to-inventory hint, set only by the ToggleItemStatus slice via `with { Promote = ... }`.
        // Not part of the positional ctor: read/projection paths (From, ToProjection) leave it null.
        public PromoteSuggestion? Promote { get; init; }

        // EF-translatable projection used by read slices. Stays simple enough for EF
        // (no method calls, no captured variables).
        public static readonly Expression<Func<ListItem, ListItemResponse>> ToProjection = i => new ListItemResponse(
            i.Id,
            i.ListId,
            i.Text,
            i.Comment,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Status,
            i.SortOrder,
            i.CreatedAt,
            i.UpdatedAt,
            i.Type,
            i.OriginalFileName,
            i.ContentType,
            i.FileSizeBytes,
            false);
    }
```
Add `using Frigorino.Domain.Entities;` if not already present (it is — `ListItem` is used). Keep the
existing `using System.Linq.Expressions;` and `using Frigorino.Features.Quantities;`.

> Note: `ExtractionPending` moved to the **end** of the positional ctor (after the media fields).
> Verify no caller passes it positionally — the only setter is `with { ExtractionPending = ... }` in
> `CreateItem.cs` (named), so this is safe. `ToProjection` passes `false` positionally as the last arg.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListItemResponseProjectionTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Build the whole solution to catch positional-ctor drift**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded (confirms `CreateItem.cs`'s `with { ExtractionPending = ... }` still compiles).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/ListItemResponse.cs \
        Application/Frigorino.Test/Features/ListItemResponseProjectionTests.cs
git commit -m "feat(features): add Type/FileName/ContentType/FileSize to ListItemResponse"
```

---

## Task 4: `CreateMediaItem` slice (multipart upload)

**Files:**
- Create: `Application/Frigorino.Features/Lists/Items/CreateMediaItem.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (register `MapCreateMediaItem`)
- Test: `Application/Frigorino.Test/Features/CreateMediaItemSliceTests.cs` (create)

**Endpoint:** `POST /api/household/{householdId}/lists/{listId}/items/media`, `multipart/form-data`
with `file` (binary), `type` (`ListItemType`), `caption` (optional).

**Orphan-safe ordering:** cheap checks (membership, list, file present, size) → process image → save
blob(s) → `AddMediaItem` → `SaveChanges`. If `AddMediaItem` fails **or** `SaveChanges` throws after
the blobs are saved, compensating-delete both blobs.

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Features/CreateMediaItemSliceTests.cs`:
```csharp
using System.Text;
using FakeItEasy;
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Lists.Items;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class CreateMediaItemSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        private static async Task<int> SeedListAsync(TestApplicationDbContext db, string userId, int householdId)
        {
            db.Households.Add(new Household { Id = householdId, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId, HouseholdId = householdId, Role = HouseholdRole.Member,
                IsActive = true, JoinedAt = DateTime.UtcNow,
            });
            var list = new List
            {
                Name = "Groceries", HouseholdId = householdId, CreatedByUserId = userId,
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            return list.Id;
        }

        private static IFormFile FakeFile(string name, long length, byte[]? content = null)
        {
            var stream = new MemoryStream(content ?? Encoding.UTF8.GetBytes("raw-bytes"));
            return new FormFile(stream, 0, length, "file", name)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg",
            };
        }

        private static IImageProcessor OkProcessor() =>
            FakeProcessor(Result.Ok(new ProcessedImage(
                new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, "image/webp", 3)));

        private static IImageProcessor FakeProcessor(Result<ProcessedImage> result)
        {
            var p = A.Fake<IImageProcessor>();
            A.CallTo(() => p.ProcessAsync(A<Stream>._, A<CancellationToken>._)).Returns(result);
            return p;
        }

        private static IFileStorage SequentialStorage(out IFileStorage storage)
        {
            storage = A.Fake<IFileStorage>();
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._))
                .ReturnsNextFromSequence("key-full", "key-thumb");
            return storage;
        }

        [Fact]
        public async Task Post_ValidImage_PersistsMediaItemAndSavesBothBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("photo.jpg", length: 2048),
                ListItemType.Image, caption: "the blue one",
                UserNamed("u1"), db, storage, OkProcessor(), CancellationToken.None);

            Assert.IsType<Created<ListItemResponse>>(result.Result);

            var row = await db.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Image, row.Type);
            Assert.Equal("key-full", row.StorageKey);
            Assert.Equal("key-thumb", row.ThumbnailStorageKey);
            Assert.Equal("image/webp", row.ContentType);
            Assert.Equal("photo.jpg", row.OriginalFileName);
            Assert.Equal("the blue one", row.Comment);
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._))
                .MustHaveHappenedTwiceExactly();
            A.CallTo(() => storage.DeleteAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, null,
                UserNamed("intruder"), db, storage, OkProcessor(), CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_OverSizeCap_Returns413_WithoutProcessing()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);
            var processor = OkProcessor();

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("big.jpg", length: ListItem.MaxFileSizeBytes + 1),
                ListItemType.Image, null,
                UserNamed("u1"), db, storage, processor, CancellationToken.None);

            var status = Assert.IsType<StatusCodeHttpResult>(result.Result);
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
            A.CallTo(() => processor.ProcessAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_UndecodableImage_Returns400_AndSavesNoBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);
            var processor = FakeProcessor(Result.Fail("bad image"));

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, null,
                UserNamed("u1"), db, storage, processor, CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_AggregateRejects_CompensatesByDeletingBothBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);

            // Caption longer than CommentMaxLength → AddMediaItem fails AFTER blobs are saved.
            var tooLong = new string('x', ListItem.CommentMaxLength + 1);

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, tooLong,
                UserNamed("u1"), db, storage, OkProcessor(), CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
            Assert.Empty(await db.ListItems.ToListAsync());
            A.CallTo(() => storage.DeleteAsync("key-full", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => storage.DeleteAsync("key-thumb", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CreateMediaItemSliceTests"`
Expected: FAIL — `CreateMediaItemEndpoint` does not exist.

- [ ] **Step 3: Implement the slice**

Create `Application/Frigorino.Features/Lists/Items/CreateMediaItem.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class CreateMediaItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateMediaItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("/media", Handle)
               .WithName("CreateMediaItem")
               .DisableAntiforgery() // API endpoint: no antiforgery token on multipart form posts.
               .Produces<ListItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status413PayloadTooLarge)
               .ProducesValidationProblem();
            return app;
        }

        public static async Task<Results<Created<ListItemResponse>, NotFound, ValidationProblem, StatusCodeHttpResult>> Handle(
            int householdId,
            int listId,
            IFormFile file,
            [FromForm] ListItemType type,
            [FromForm] string? caption,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IFileStorage storage,
            IImageProcessor imageProcessor,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            if (file is null || file.Length <= 0)
            {
                return new Error("A file is required.").WithProperty("file").ToValidationProblemResult();
            }

            // Transport guard before any expensive work; FormOptions/Kestrel is the hard backstop.
            if (file.Length > ListItem.MaxFileSizeBytes)
            {
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            Result<ProcessedImage> processed;
            await using (var upload = file.OpenReadStream())
            {
                processed = await imageProcessor.ProcessAsync(upload, ct);
            }
            if (processed.IsFailed)
            {
                return processed.ToValidationProblem();
            }

            // Orphan-safe ordering: save blobs BEFORE the row exists; compensate on any later failure.
            var storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
            var thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);

            try
            {
                var stored = new StoredFile(
                    storageKey, thumbnailKey, processed.Value.ContentType,
                    file.FileName, processed.Value.FullResSizeBytes);

                var result = list.AddMediaItem(type, caption, stored);
                if (result.IsFailed)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey);
                    return result.ToValidationProblem();
                }

                await db.SaveChangesAsync(ct);

                return TypedResults.Created(
                    $"/api/household/{householdId}/lists/{listId}/items/{result.Value.Id}",
                    ListItemResponse.From(result.Value));
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey);
                throw;
            }
        }

        // Best-effort cleanup of just-uploaded blobs; DeleteAsync is idempotent.
        private static async Task CompensateAsync(IFileStorage storage, string storageKey, string thumbnailKey)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            await storage.DeleteAsync(thumbnailKey, CancellationToken.None);
        }
    }
}
```

- [ ] **Step 4: Add the `ToValidationProblemResult` helper used above**

`Handle` returns a 4-member `Results<...>` union, so a bare `ValidationProblem` from the
file-missing branch must be wrapped. Add this overload to
`Application/Frigorino.Features/Results/ResultExtensions.cs` (inside the class):
```csharp
        // Convenience for slices that build a one-off ValidationProblem from a single Error without
        // going through a Result<T> (e.g. transport-level guards).
        public static ValidationProblem ToValidationProblemResult(this Error error)
        {
            var key = error.Metadata.TryGetValue(PropertyMetadataKey, out var p)
                ? p?.ToString() ?? string.Empty
                : string.Empty;
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { [key] = new[] { error.Message } });
        }
```
Add `using FluentResults;` (already present) — `Error` is from FluentResults.

- [ ] **Step 5: Register the endpoint in Program.cs**

In `Application/Frigorino.Web/Program.cs`, in the `listItems` group block, add after
`listItems.MapCreateItem();`:
```csharp
listItems.MapCreateMediaItem();
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CreateMediaItemSliceTests"`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/CreateMediaItem.cs \
        Application/Frigorino.Features/Results/ResultExtensions.cs \
        Application/Frigorino.Web/Program.cs \
        Application/Frigorino.Test/Features/CreateMediaItemSliceTests.cs
git commit -m "feat(features): add CreateMediaItem multipart slice (orphan-safe + compensating delete)"
```

---

## Task 5: `GetItemFile` + `GetItemThumbnail` slices (byte serving)

**Files:**
- Create: `Application/Frigorino.Features/Lists/Items/GetItemFile.cs`
- Create: `Application/Frigorino.Features/Lists/Items/GetItemThumbnail.cs`
- Modify: `Application/Frigorino.Web/Program.cs`
- Test: `Application/Frigorino.Test/Features/GetItemFileSliceTests.cs` (create)

Both stream bytes via `IFileStorage.OpenAsync`, reading `ContentType` from the DB row (the port is
lean by design). Membership-gated; 404 when the item / relevant key is missing.

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Features/GetItemFileSliceTests.cs`:
```csharp
using System.Text;
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Lists.Items;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class GetItemFileSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        private static async Task<(int listId, int itemId)> SeedImageItemAsync(TestApplicationDbContext db, string userId)
        {
            db.Households.Add(new Household { Id = 1, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId, HouseholdId = 1, Role = HouseholdRole.Member,
                IsActive = true, JoinedAt = DateTime.UtcNow,
            });
            var list = new List { Name = "L", HouseholdId = 1, CreatedByUserId = userId, IsActive = true };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            var item = new ListItem
            {
                ListId = list.Id, Type = ListItemType.Image, Text = "",
                StorageKey = "full-key", ThumbnailStorageKey = "thumb-key",
                ContentType = "image/webp", OriginalFileName = "p.jpg", FileSizeBytes = 3, IsActive = true,
            };
            db.ListItems.Add(item);
            await db.SaveChangesAsync();
            return (list.Id, item.Id);
        }

        [Fact]
        public async Task GetFile_Member_StreamsBytesWithContentType()
        {
            using var db = NewContext();
            var (listId, itemId) = await SeedImageItemAsync(db, "u1");
            var storage = A.Fake<IFileStorage>();
            A.CallTo(() => storage.OpenAsync("full-key", A<CancellationToken>._))
                .Returns<Stream?>(new MemoryStream(Encoding.UTF8.GetBytes("img")));

            var result = await GetItemFileEndpoint.Handle(
                1, listId, itemId, UserNamed("u1"), db, storage, new DefaultHttpContext(), CancellationToken.None);

            var file = Assert.IsType<FileStreamHttpResult>(result.Result);
            Assert.Equal("image/webp", file.ContentType);
        }

        [Fact]
        public async Task GetFile_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var (listId, itemId) = await SeedImageItemAsync(db, "u1");
            var storage = A.Fake<IFileStorage>();

            var result = await GetItemFileEndpoint.Handle(
                1, listId, itemId, UserNamed("intruder"), db, storage, new DefaultHttpContext(), CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task GetThumbnail_MissingBlob_ReturnsNotFound()
        {
            using var db = NewContext();
            var (listId, itemId) = await SeedImageItemAsync(db, "u1");
            var storage = A.Fake<IFileStorage>();
            A.CallTo(() => storage.OpenAsync("thumb-key", A<CancellationToken>._)).Returns<Stream?>(null);

            var result = await GetItemThumbnailEndpoint.Handle(
                1, listId, itemId, UserNamed("u1"), db, storage, new DefaultHttpContext(), CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~GetItemFileSliceTests"`
Expected: FAIL — endpoints do not exist.

- [ ] **Step 3: Implement `GetItemFile`**

Create `Application/Frigorino.Features/Lists/Items/GetItemFile.cs`:
```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class GetItemFileEndpoint
    {
        public static IEndpointRouteBuilder MapGetItemFile(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}/file", Handle)
               .WithName("GetItemFile")
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound>> Handle(
            int householdId,
            int listId,
            int itemId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IFileStorage storage,
            HttpContext http,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var item = await db.ListItems
                .Where(i => i.Id == itemId && i.ListId == listId && i.IsActive
                    && i.List.HouseholdId == householdId && i.List.IsActive)
                .Select(i => new { i.StorageKey, i.ContentType, i.OriginalFileName })
                .FirstOrDefaultAsync(ct);
            if (item is null || string.IsNullOrEmpty(item.StorageKey))
            {
                return TypedResults.NotFound();
            }

            var stream = await storage.OpenAsync(item.StorageKey, ct);
            if (stream is null)
            {
                return TypedResults.NotFound();
            }

            // Content-addressable GUID keys never change → cache hard.
            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(
                stream,
                item.ContentType ?? "application/octet-stream",
                fileDownloadName: SanitizeFileName(item.OriginalFileName));
        }

        // Strip path separators / control chars so OriginalFileName can't inject into Content-Disposition.
        private static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '/' && c != '\\' && c != '"').ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }
    }
}
```

- [ ] **Step 4: Implement `GetItemThumbnail`**

Create `Application/Frigorino.Features/Lists/Items/GetItemThumbnail.cs`:
```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class GetItemThumbnailEndpoint
    {
        public static IEndpointRouteBuilder MapGetItemThumbnail(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}/thumbnail", Handle)
               .WithName("GetItemThumbnail")
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound>> Handle(
            int householdId,
            int listId,
            int itemId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IFileStorage storage,
            HttpContext http,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var item = await db.ListItems
                .Where(i => i.Id == itemId && i.ListId == listId && i.IsActive
                    && i.List.HouseholdId == householdId && i.List.IsActive)
                .Select(i => new { i.ThumbnailStorageKey, i.ContentType })
                .FirstOrDefaultAsync(ct);
            if (item is null || string.IsNullOrEmpty(item.ThumbnailStorageKey))
            {
                return TypedResults.NotFound();
            }

            var stream = await storage.OpenAsync(item.ThumbnailStorageKey, ct);
            if (stream is null)
            {
                return TypedResults.NotFound();
            }

            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(stream, item.ContentType ?? "application/octet-stream");
        }
    }
}
```

- [ ] **Step 5: Register both endpoints in Program.cs**

In the `listItems` group, after `listItems.MapCreateMediaItem();`:
```csharp
listItems.MapGetItemFile();
listItems.MapGetItemThumbnail();
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~GetItemFileSliceTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/GetItemFile.cs \
        Application/Frigorino.Features/Lists/Items/GetItemThumbnail.cs \
        Application/Frigorino.Web/Program.cs \
        Application/Frigorino.Test/Features/GetItemFileSliceTests.cs
git commit -m "feat(features): add GetItemFile + GetItemThumbnail byte-serving slices"
```

---

## Task 6: Regenerate the TypeScript API client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `src/lib/api/**`

- [ ] **Step 1: Regenerate**

Run:
```bash
cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run api
```
Expected: rebuilds the backend, emits `openapi.json`, regenerates `src/lib/api/`. No errors.

- [ ] **Step 2: Verify the generated surface**

Confirm these now exist (grep is fine here — generated output):
```bash
cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp
grep -rl "createMediaItem" src/lib/api
grep -rl "getItemThumbnail\|getItemFile" src/lib/api
```
Expected: matches in `sdk.gen.ts` and `@tanstack/react-query.gen.ts`. Confirm `types.gen.ts`
contains a `ListItemType` (string union `"Text" | "Image" | "Document"`) and that
`ListItemResponse` now has `type`, `fileName`, `contentType`, `fileSize`. Confirm the
`CreateMediaItem` request body type has a `file` property typed as `Blob | File`.

> If `createMediaItem` did **not** generate a multipart body with a `file: Blob | File` property,
> stop and inspect `openapi.json` for the `/items/media` operation — the `requestBody` must be
> `multipart/form-data` with `file` as `type: string, format: binary`. (It should, from the
> `IFormFile` + `[FromForm]` binding. If not, the `CreateMediaItem` endpoint binding needs revisiting
> before continuing.)

- [ ] **Step 3: Commit the regenerated client**

```bash
cd C:/Repositories/frigorino
git add Application/Frigorino.Web/ClientApp/src/lib/openapi.json Application/Frigorino.Web/ClientApp/src/lib/api
git commit -m "chore(api): regenerate client for media item endpoints"
```

---

## Task 7: Frontend hooks — `useItemImage` + `useCreateMediaItem`

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useItemImage.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useCreateMediaItem.ts`

> No JS test runner exists; verification for frontend tasks is `npm run tsc` + `npm run lint` (and the
> integration tests in Task 12). Run them after each frontend task.

- [ ] **Step 1: Create the auth-blob hook**

The byte endpoints need the Firebase bearer (a plain `<img src>` can't carry it), so we fetch through
the configured `client` as a Blob and hand back an object URL. This is the one justified deviation
from the generated-JSON-hook convention (binary + object-URL lifecycle).

Create `Application/Frigorino.Web/ClientApp/src/features/lists/items/useItemImage.ts`:
```ts
import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { client } from "../../../lib/api/client.gen";

type Variant = "thumbnail" | "file";

// Fetches an item's image bytes (auth'd, via the configured client) as an object URL.
// Cached by item id + variant; the URL is revoked on unmount / when the query is cleaned up.
export const useItemImage = (
    householdId: number,
    listId: number,
    itemId: number,
    variant: Variant,
    enabled = true,
) => {
    const query = useQuery({
        queryKey: ["item-image", householdId, listId, itemId, variant],
        enabled: enabled && householdId > 0 && listId > 0 && itemId > 0,
        staleTime: Infinity,
        gcTime: 5 * 60 * 1000,
        queryFn: async () => {
            const { data, error } = await client.get({
                url: `/api/household/${householdId}/lists/${listId}/items/${itemId}/${variant}`,
                parseAs: "blob",
            });
            if (error || !data) {
                throw new Error("Failed to load image");
            }
            return URL.createObjectURL(data as Blob);
        },
    });

    // Revoke the object URL when this consumer unmounts or the URL changes.
    useEffect(() => {
        const url = query.data;
        return () => {
            if (url) {
                URL.revokeObjectURL(url);
            }
        };
    }, [query.data]);

    return query;
};
```

> Verify the `client.get({ url, parseAs: "blob" })` call against the generated `client.gen.ts` API. If
> the configured client exposes a different low-level signature, adapt to it (the requirement is: a
> GET to the URL, with the request interceptor's bearer applied, returning the response body as a
> Blob). Do **not** bypass the configured `client` (that would drop the auth interceptor).

- [ ] **Step 2: Create the multipart create hook**

Create `Application/Frigorino.Web/ClientApp/src/features/lists/items/useCreateMediaItem.ts`:
```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createMediaItemMutation,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

// Arg-less, per the hook convention. Caller passes
//   { path: { householdId, listId }, body: { file, type, caption } }.
// hey-api serializes the body via formDataBodySerializer (FormData); do NOT set Content-Type — the
// browser sets the multipart boundary. No optimistic insert (uploads show progress in the sheet);
// invalidate the items query on success.
export const useCreateMediaItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createMediaItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
        },
    });
};
```

- [ ] **Step 3: Type-check + lint**

Run:
```bash
cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint
```
Expected: no errors. (If `createMediaItemMutation`'s `body` type names differ, align the call sites in
Task 10 accordingly — the names come from the generated client.)

- [ ] **Step 4: Commit**

```bash
cd C:/Repositories/frigorino
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useItemImage.ts \
        Application/Frigorino.Web/ClientApp/src/features/lists/items/useCreateMediaItem.ts
git commit -m "feat(web): add useItemImage (auth blob) + useCreateMediaItem hooks"
```

---

## Task 8: Renderer switch — Text/Image renderers + lightbox

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/TextItemRenderer.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ImageItemRenderer.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ImageLightbox.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx`

`ListItemContent` becomes a thin switch on `item.type`. The current text logic moves verbatim into
`TextItemRenderer`.

- [ ] **Step 1: Extract the current text logic into `TextItemRenderer`**

Create `TextItemRenderer.tsx` with the **entire current body** of `ListItemContent.tsx` (the
`ListItemText` render, `renderTextWithLinks`, `URL_REGEX`, `isImageUrl`, `InlineImage`) — renamed.
Keep the `Props` interface (`item`, `onEditQuantity?`, `onEditComment?`). Header:
```tsx
import { Box, Link, ListItemText, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import { useLongPress } from "../../../../hooks/useLongPress";
import type { ListItemResponse } from "../../../../lib/api";

interface Props {
    item: ListItemResponse;
    onEditQuantity?: () => void;
    onEditComment?: () => void;
}

export function TextItemRenderer({ item, onEditQuantity, onEditComment }: Props) {
    // ... move the existing ListItemContent function body here verbatim ...
}

// ... move renderTextWithLinks / URL_REGEX / IMAGE_EXTENSIONS / isImageUrl / InlineImage here verbatim ...
```

- [ ] **Step 2: Create `ImageLightbox`**

Create `ImageLightbox.tsx`:
```tsx
import { Close } from "@mui/icons-material";
import {
    Box,
    CircularProgress,
    Dialog,
    IconButton,
    Typography,
} from "@mui/material";
import { useItemImage } from "../useItemImage";

interface Props {
    householdId: number;
    listId: number;
    itemId: number;
    caption?: string | null;
    open: boolean;
    onClose: () => void;
}

export function ImageLightbox({
    householdId,
    listId,
    itemId,
    caption,
    open,
    onClose,
}: Props) {
    const { data: url, isLoading } = useItemImage(
        householdId,
        listId,
        itemId,
        "file",
        open,
    );

    return (
        <Dialog open={open} onClose={onClose} maxWidth="lg" data-testid="image-lightbox">
            <Box sx={{ position: "relative", bgcolor: "common.black" }}>
                <IconButton
                    onClick={onClose}
                    aria-label="close"
                    sx={{ position: "absolute", top: 8, right: 8, color: "common.white", zIndex: 1 }}
                >
                    <Close />
                </IconButton>
                {isLoading || !url ? (
                    <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: 240, minWidth: 240 }}>
                        <CircularProgress sx={{ color: "common.white" }} />
                    </Box>
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={caption ?? ""}
                        sx={{ display: "block", maxWidth: "90vw", maxHeight: "85vh", width: "auto", height: "auto" }}
                    />
                )}
            </Box>
            {caption ? (
                <Typography variant="body2" sx={{ p: 1.5, color: "text.secondary" }}>
                    {caption}
                </Typography>
            ) : null}
        </Dialog>
    );
}
```

- [ ] **Step 3: Create `ImageItemRenderer`**

Create `ImageItemRenderer.tsx`:
```tsx
import { BrokenImage } from "@mui/icons-material";
import { Box, Skeleton, Typography } from "@mui/material";
import { useState } from "react";
import type { ListItemResponse } from "../../../../lib/api";
import { useCurrentHousehold } from "../../../me/activeHousehold/useCurrentHousehold";
import { useItemImage } from "../useItemImage";
import { ImageLightbox } from "./ImageLightbox";

interface Props {
    item: ListItemResponse;
}

const THUMB_SIZE = 56;

export function ImageItemRenderer({ item }: Props) {
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;
    const [lightboxOpen, setLightboxOpen] = useState(false);

    const {
        data: url,
        isLoading,
        isError,
    } = useItemImage(householdId, item.listId, item.id, "thumbnail");

    return (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flex: 1, minWidth: 0 }}>
            <Box
                role="button"
                tabIndex={0}
                aria-label="open image"
                data-testid={`list-item-image-${item.id}`}
                onClick={() => setLightboxOpen(true)}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        setLightboxOpen(true);
                    }
                }}
                sx={{
                    width: THUMB_SIZE,
                    height: THUMB_SIZE,
                    flexShrink: 0,
                    borderRadius: 1,
                    overflow: "hidden",
                    cursor: "pointer",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                }}
            >
                {isLoading ? (
                    <Skeleton variant="rectangular" width={THUMB_SIZE} height={THUMB_SIZE} />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={item.comment ?? ""}
                        sx={{ width: "100%", height: "100%", objectFit: "cover" }}
                    />
                )}
            </Box>

            {item.comment ? (
                <Typography
                    variant="body2"
                    sx={{ wordBreak: "break-word", color: "text.secondary", flex: 1, minWidth: 0 }}
                >
                    {item.comment}
                </Typography>
            ) : null}

            {householdId > 0 ? (
                <ImageLightbox
                    householdId={householdId}
                    listId={item.listId}
                    itemId={item.id}
                    caption={item.comment}
                    open={lightboxOpen}
                    onClose={() => setLightboxOpen(false)}
                />
            ) : null}
        </Box>
    );
}
```

- [ ] **Step 4: Turn `ListItemContent` into the switch**

Replace the entire contents of `ListItemContent.tsx` with:
```tsx
import type { ListItemResponse } from "../../../../lib/api";
import { ImageItemRenderer } from "./ImageItemRenderer";
import { TextItemRenderer } from "./TextItemRenderer";

interface Props {
    item: ListItemResponse;
    onEditQuantity?: () => void;
    onEditComment?: () => void;
}

// Renderer switch keyed by item.type. Document renderer arrives in sub-feature #3.
export function ListItemContent({ item, onEditQuantity, onEditComment }: Props) {
    if (item.type === "Image") {
        return <ImageItemRenderer item={item} />;
    }
    return (
        <TextItemRenderer
            item={item}
            onEditQuantity={onEditQuantity}
            onEditComment={onEditComment}
        />
    );
}
```

> `item.type` is the generated string union (`"Text" | "Image" | "Document"`). Confirm the exact
> casing in `types.gen.ts` from Task 6 and match it.

- [ ] **Step 5: Type-check + lint**

Run: `cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 6: Commit**

```bash
cd C:/Repositories/frigorino
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/TextItemRenderer.tsx \
        Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ImageItemRenderer.tsx \
        Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ImageLightbox.tsx \
        Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx
git commit -m "feat(web): renderer switch (Text/Image) + image thumbnail + lightbox"
```

---

## Task 9: Composer attach action-feature

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/features/attachComposerFeature.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/components/composer/index.ts` (export it)

The attach affordance is a Composer **action-feature**: a "+" button that opens a menu (Photo live,
Document disabled), with a hidden file input. Picking a file calls `complete({ file })`, which routes
up through `onComplete` to the list (Task 10 opens the preview sheet).

- [ ] **Step 1: Create the feature**

Create `attachComposerFeature.tsx`:
```tsx
/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside a local presentational component. */
import { AddPhotoAlternate, Description, Image } from "@mui/icons-material";
import { IconButton, ListItemIcon, ListItemText, Menu, MenuItem } from "@mui/material";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { defineAction } from "../defineFeature";
import type { ActionContext } from "../types";

// Payload the attach action emits up via onComplete (kind: "attach").
export interface AttachPayload {
    file: File;
}

const AttachTrigger = ({ complete, disabled }: ActionContext<AttachPayload>) => {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<null | HTMLElement>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const handlePick = (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        // Reset so picking the same file again re-fires change.
        event.target.value = "";
        if (file) {
            complete({ file });
        }
    };

    return (
        <>
            <IconButton
                onClick={(e) => setAnchor(e.currentTarget)}
                disabled={disabled}
                aria-label={t("lists.attach")}
                data-testid="composer-attach-button"
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <AddPhotoAlternate fontSize="small" />
            </IconButton>

            <Menu anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)}>
                <MenuItem
                    data-testid="composer-attach-photo"
                    onClick={() => {
                        setAnchor(null);
                        fileInputRef.current?.click();
                    }}
                >
                    <ListItemIcon><Image fontSize="small" /></ListItemIcon>
                    <ListItemText>{t("lists.attachPhoto")}</ListItemText>
                </MenuItem>
                {/* Document arrives in sub-feature #3. */}
                <MenuItem disabled data-testid="composer-attach-document">
                    <ListItemIcon><Description fontSize="small" /></ListItemIcon>
                    <ListItemText>{t("lists.attachDocument")}</ListItemText>
                </MenuItem>
            </Menu>

            <input
                ref={fileInputRef}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                capture="environment"
                hidden
                data-testid="composer-attach-file-input"
                onChange={handlePick}
            />
        </>
    );
};

export const attachComposerFeature = defineAction<"attach", AttachPayload>({
    id: "attach",
    renderTrigger: (ctx) => <AttachTrigger {...ctx} />,
});
```

- [ ] **Step 2: Export from the composer barrel**

In `Application/Frigorino.Web/ClientApp/src/components/composer/index.ts`, add an export for
`attachComposerFeature` and the `AttachPayload` type, matching the existing export style for
`commentComposerFeature` / `quantityComposerFeature`. (Open the file and mirror the existing lines.)

- [ ] **Step 3: Type-check + lint**

Run: `cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
cd C:/Repositories/frigorino
git add Application/Frigorino.Web/ClientApp/src/components/composer/features/attachComposerFeature.tsx \
        Application/Frigorino.Web/ClientApp/src/components/composer/index.ts
git commit -m "feat(web): composer attach action-feature (photo live, document disabled)"
```

---

## Task 10: Media preview sheet + wiring into the list

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/MediaPreviewSheet.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx`

Flow: attach feature emits `{ kind: "attach", file }` → `ListFooter.handleComplete` calls a new
`onAttachFile(file)` prop → `ListViewPage` stores the picked file and opens `MediaPreviewSheet` →
Send calls `useCreateMediaItem` with `type: "Image"` + caption.

- [ ] **Step 1: Create `MediaPreviewSheet`**

Create `MediaPreviewSheet.tsx`:
```tsx
import { Close, Send } from "@mui/icons-material";
import {
    Box,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    IconButton,
    TextField,
} from "@mui/material";
import { LoadingButton } from "@mui/lab";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { COMMENT_MAX_LENGTH } from "../../../../components/composer/features/commentComposerFeature";

interface Props {
    file: File | null;
    isUploading: boolean;
    onSend: (caption: string | null) => void;
    onClose: () => void;
}

export function MediaPreviewSheet({ file, isUploading, onSend, onClose }: Props) {
    const { t } = useTranslation();
    const [caption, setCaption] = useState("");

    // Local object URL for the picked file (no server round-trip for the preview).
    const previewUrl = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
    useEffect(() => {
        return () => {
            if (previewUrl) {
                URL.revokeObjectURL(previewUrl);
            }
        };
    }, [previewUrl]);

    // Reset caption whenever a new file is opened.
    useEffect(() => {
        setCaption("");
    }, [file]);

    return (
        <Dialog open={Boolean(file)} onClose={isUploading ? undefined : onClose} fullWidth maxWidth="xs" data-testid="media-preview-sheet">
            <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                {t("lists.attachPhoto")}
                <IconButton onClick={onClose} disabled={isUploading} aria-label={t("common.cancel")}>
                    <Close />
                </IconButton>
            </DialogTitle>
            <DialogContent>
                {previewUrl ? (
                    <Box
                        component="img"
                        src={previewUrl}
                        alt=""
                        sx={{ width: "100%", maxHeight: "50vh", objectFit: "contain", borderRadius: 1, mb: 2 }}
                    />
                ) : null}
                <TextField
                    fullWidth
                    multiline
                    minRows={1}
                    maxRows={4}
                    size="small"
                    placeholder={t("lists.captionPlaceholder")}
                    value={caption}
                    onChange={(e) => setCaption(e.target.value)}
                    disabled={isUploading}
                    slotProps={{ htmlInput: { maxLength: COMMENT_MAX_LENGTH, "data-testid": "media-caption-input" } }}
                />
            </DialogContent>
            <DialogActions>
                <LoadingButton
                    variant="contained"
                    loading={isUploading}
                    startIcon={<Send />}
                    onClick={() => onSend(caption.trim() || null)}
                    data-testid="media-send-button"
                >
                    {t("common.send")}
                </LoadingButton>
            </DialogActions>
        </Dialog>
    );
}
```

> If `@mui/lab`'s `LoadingButton` is not already a dependency, replace it with a plain MUI `Button`
> plus a `disabled={isUploading}` and a `<CircularProgress size={16} />` start icon. Check
> `package.json` before adding a dependency (project policy: prefer existing deps).

- [ ] **Step 2: Add the attach feature + `onAttachFile` to `ListFooter`**

In `ListFooter.tsx`:
1. Import the attach feature + payload type:
```tsx
import {
    Composer,
    attachComposerFeature,
    commentComposerFeature,
    draftToQuantity,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
```
2. Add the attach feature to the **add** feature set (not edit):
```tsx
const ADD_FEATURES = [commentComposerFeature, attachComposerFeature] as const;
```
3. Add `onAttachFile: (file: File) => void;` to `ListFooterProps`.
4. Destructure `onAttachFile` in the component args.
5. Handle the `"attach"` completion kind in `handleComplete` (it's a discriminated union; the attach
   variant has `kind: "attach"` and `file`):
```tsx
const handleComplete = useCallback(
    (r: Completion<typeof EDIT_FEATURES>) => {
        if (r.kind === "attach") {
            onAttachFile(r.file);
            return;
        }
        if (r.mode === "edit") {
            onUpdateItem(r.text, draftToQuantity(r.quantity), r.comment.trim());
        } else {
            onAddItem(r.text, r.comment.trim() || null);
            onScrollToLastUnchecked();
        }
    },
    [onAddItem, onUpdateItem, onScrollToLastUnchecked, onAttachFile],
);
```

> `Completion<typeof EDIT_FEATURES>` does not include the attach action variant (attach is only in
> `ADD_FEATURES`). Widen the `handleComplete` parameter type to include it, e.g. type it as
> `Completion<typeof ADD_FEATURES> | Completion<typeof EDIT_FEATURES>`, or narrow on `"kind" in r`.
> Pick whichever keeps `npm run tsc` green; the attach branch must be reachable when not editing.

- [ ] **Step 3: Wire the sheet + upload in `ListViewPage`**

In `ListViewPage.tsx`:
1. Imports:
```tsx
import { MediaPreviewSheet } from "../items/components/MediaPreviewSheet";
import { useCreateMediaItem } from "../items/useCreateMediaItem";
```
2. State + hook (near the other hooks):
```tsx
const [pendingFile, setPendingFile] = useState<File | null>(null);
const createMediaMutation = useCreateMediaItem();
```
3. Handlers:
```tsx
const handleAttachFile = useCallback((file: File) => {
    setPendingFile(file);
}, []);

const handleSendMedia = useCallback(
    async (caption: string | null) => {
        if (!householdId || !pendingFile) return;
        try {
            await createMediaMutation.mutateAsync({
                path: { householdId, listId: listIdNum },
                body: { file: pendingFile, type: "Image", caption },
            });
            setPendingFile(null);
            scrollToLastUncheckedItem();
        } catch {
            // Mutation surfaces the error; keep the sheet open so the user can retry/cancel.
        }
    },
    [householdId, listIdNum, pendingFile, createMediaMutation, scrollToLastUncheckedItem],
);
```
4. Pass `onAttachFile={handleAttachFile}` to `<ListFooter ... />`.
5. Render the sheet before the closing `</Box>`:
```tsx
<MediaPreviewSheet
    file={pendingFile}
    isUploading={createMediaMutation.isPending}
    onSend={handleSendMedia}
    onClose={() => setPendingFile(null)}
/>
```

> Confirm the generated `body` field names for `createMediaItem` (`file` / `type` / `caption`) from
> Task 6 and match them exactly here. `type: "Image"` must match the generated `ListItemType` union
> member casing.

- [ ] **Step 4: Type-check + lint**

Run: `cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
cd C:/Repositories/frigorino
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/MediaPreviewSheet.tsx \
        Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx \
        Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx
git commit -m "feat(web): media preview sheet + wire photo upload into the list"
```

---

## Task 11: i18n strings

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the keys**

Add under the existing `lists` object in **en**:
```json
"attach": "Attach",
"attachPhoto": "Photo",
"attachDocument": "Document",
"captionPlaceholder": "Add a caption (optional)"
```
And ensure `common.send` exists (`"send": "Send"`) and `common.cancel` exists; add if missing.

In **de**:
```json
"attach": "Anhängen",
"attachPhoto": "Foto",
"attachDocument": "Dokument",
"captionPlaceholder": "Bildunterschrift hinzufügen (optional)"
```
And `common.send` → `"send": "Senden"`, `common.cancel` if missing.

> Match the existing nesting/order; do not reformat unrelated keys. Tests never assert on these
> strings (testids only), so only the keys must exist.

- [ ] **Step 2: Validate JSON**

Run: `cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run tsc`
(Then a quick visual check that both JSON files still parse — `npm run lint` covers JSON-in-TS, not
these files; rely on the dev server / build in Task 12 to catch a malformed file.)

- [ ] **Step 3: Commit**

```bash
cd C:/Repositories/frigorino
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json \
        Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(web): i18n strings for attach photo / caption"
```

---

## Task 12: Integration tests + full verification gate

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItemSteps.cs`

Integration uses the **real** `ImageSharpImageProcessor` and **real** `LocalFileStorage` (temp dir) —
only the AI classifiers stay stubbed (existing). A tiny generated PNG is uploaded via Playwright's
file chooser; the byte endpoints are checked via `APIRequest.GetAsync`.

- [ ] **Step 1: Bind `LocalFileStorage` to a temp dir in the test factory**

In `TestWebApplicationFactory.cs`, inside the existing `builder.ConfigureServices(services => { ... })`
block (alongside the `RemoveAll<IItemClassifier>()` lines), add:
```csharp
services.RemoveAll<IFileStorage>();
var blobRoot = Path.Combine(Path.GetTempPath(), "frigorino-it-blobs", Guid.NewGuid().ToString("N"));
services.AddSingleton<IFileStorage>(new LocalFileStorage(blobRoot));
```
Add `using Frigorino.Domain.Interfaces;` and `using Frigorino.Infrastructure.Services;` if missing.
(Leave `IImageProcessor` as the real registration — we want the genuine pipeline.)

- [ ] **Step 2: Add a multipart upload + byte-GET helper to `TestApiClient`**

Add to `TestApiClient.cs`:
```csharp
// 1x1 PNG (valid image bytes) for upload scenarios.
private static readonly byte[] TinyPng = Convert.FromBase64String(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

public Task<IAPIResponse> TryUploadImageAsync(int listId, string caption = "", int? householdId = null)
{
    var targetHouseholdId = householdId ?? ctx.HouseholdId;
    var form = ctx.BrowserContext.APIRequest.CreateFormData();
    form.Append("file", new FilePayload { Name = "photo.png", MimeType = "image/png", Buffer = TinyPng });
    form.Append("type", "Image");
    form.Append("caption", caption);
    return ctx.BrowserContext.APIRequest.PostAsync(
        $"/api/household/{targetHouseholdId}/lists/{listId}/items/media",
        new APIRequestContextOptions { Headers = AuthHeaders, Multipart = form });
}
```
Add `using Microsoft.Playwright;` if not already present (for `FilePayload`).

> `IAPIRequestContext.CreateFormData()` + `IFormData.Append(string, FilePayload)` + the
> `APIRequestContextOptions.Multipart` property are the documented Playwright .NET multipart API. If
> the installed Playwright version differs, fall back to driving the upload **only** through the UI
> step (Step 4) and asserting the byte endpoints via `TryGetItemFileAsync`; then drop the
> `.Api.feature` scenario. Verify against the installed `Microsoft.Playwright` before committing.

Also add simple byte-GET helpers:
```csharp
public Task<IAPIResponse> TryGetItemThumbnailAsync(int listId, int itemId, int? householdId = null)
{
    var h = householdId ?? ctx.HouseholdId;
    return ctx.BrowserContext.APIRequest.GetAsync(
        $"/api/household/{h}/lists/{listId}/items/{itemId}/thumbnail",
        new APIRequestContextOptions { Headers = AuthHeaders });
}

public Task<IAPIResponse> TryGetItemFileAsync(int listId, int itemId, int? householdId = null)
{
    var h = householdId ?? ctx.HouseholdId;
    return ctx.BrowserContext.APIRequest.GetAsync(
        $"/api/household/{h}/lists/{listId}/items/{itemId}/file",
        new APIRequestContextOptions { Headers = AuthHeaders });
}
```

- [ ] **Step 3: API-level feature**

Create `MediaItems.Api.feature`:
```gherkin
Feature: Media Items API

  Background:
    Given I am logged in with an active household

  Scenario: Uploading a photo stores it and serves both renditions
    Given there is a list named "Trip"
    When I upload a photo with caption "beach" to "Trip" via the API
    Then the API response status is 201
    And the uploaded item in "Trip" serves a thumbnail with content-type "image/webp"
    And the uploaded item in "Trip" serves a file with content-type "image/webp"
```

- [ ] **Step 4: UI feature**

Create `MediaItems.feature`:
```gherkin
Feature: Media Items

  Background:
    Given I am logged in with an active household

  Scenario: User attaches a photo and views it
    Given there is a list named "Trip"
    When I open the list "Trip"
    And I attach a photo with caption "beach"
    Then a photo thumbnail appears in the list
    When I open the photo
    Then the image lightbox is shown
```

- [ ] **Step 5: Steps**

Create `MediaItemSteps.cs`. Implement (using the patterns from `ListItemSteps.cs` /
`ListItemApiSteps.cs` and the helpers above):
```csharp
using Frigorino.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Reqnroll;

namespace Frigorino.IntegrationTests.Slices.Lists
{
    [Binding]
    public class MediaItemSteps
    {
        private readonly ScenarioContextHolder ctx;
        private readonly TestApiClient api;

        public MediaItemSteps(ScenarioContextHolder ctx, TestApiClient api)
        {
            this.ctx = ctx;
            this.api = api;
        }

        // 1x1 PNG.
        private static readonly byte[] TinyPng = System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        [When("I upload a photo with caption {string} to {string} via the API")]
        public async Task WhenIUploadAPhotoViaTheApi(string caption, string listName)
        {
            var listId = ctx.ListIds[listName];
            ctx.LastApiResponse = await api.TryUploadImageAsync(listId, caption);
            if (ctx.LastApiResponse.Ok)
            {
                var json = await ctx.LastApiResponse.JsonAsync();
                ctx.SetListItemId(listName, "__photo__", json!.Value.GetProperty("id").GetInt32());
            }
        }

        [Then("the uploaded item in {string} serves a thumbnail with content-type {string}")]
        public async Task ThenServesThumbnail(string listName, string contentType)
        {
            var listId = ctx.ListIds[listName];
            var itemId = ctx.GetListItemId(listName, "__photo__");
            var resp = await api.TryGetItemThumbnailAsync(listId, itemId);
            Assert.Equal(200, resp.Status);
            Assert.Contains(contentType, resp.Headers["content-type"]);
        }

        [Then("the uploaded item in {string} serves a file with content-type {string}")]
        public async Task ThenServesFile(string listName, string contentType)
        {
            var listId = ctx.ListIds[listName];
            var itemId = ctx.GetListItemId(listName, "__photo__");
            var resp = await api.TryGetItemFileAsync(listId, itemId);
            Assert.Equal(200, resp.Status);
            Assert.Contains(contentType, resp.Headers["content-type"]);
        }

        [When("I attach a photo with caption {string}")]
        public async Task WhenIAttachAPhoto(string caption)
        {
            await ctx.Page.GetByTestId("composer-attach-button").ClickAsync();
            await ctx.Page.GetByTestId("composer-attach-photo").ClickAsync();
            await ctx.Page.GetByTestId("composer-attach-file-input").SetInputFilesAsync(new FilePayload
            {
                Name = "photo.png",
                MimeType = "image/png",
                Buffer = TinyPng,
            });
            await ctx.Page.GetByTestId("media-caption-input").FillAsync(caption);

            var responseTask = ctx.Page.WaitForResponseAsync(r =>
                r.Url.EndsWith("/items/media") && r.Request.Method == "POST" && r.Status == 201);
            await ctx.Page.GetByTestId("media-send-button").ClickAsync();
            await responseTask;
        }

        [Then("a photo thumbnail appears in the list")]
        public async Task ThenThumbnailAppears()
        {
            await Assertions.Expect(
                ctx.Page.Locator("[data-testid^='list-item-image-']").First).ToBeVisibleAsync();
        }

        [When("I open the photo")]
        public async Task WhenIOpenThePhoto()
        {
            await ctx.Page.Locator("[data-testid^='list-item-image-']").First.ClickAsync();
        }

        [Then("the image lightbox is shown")]
        public async Task ThenLightboxShown()
        {
            await Assertions.Expect(ctx.Page.GetByTestId("image-lightbox")).ToBeVisibleAsync();
        }
    }
}
```
Reuse the existing `Given there is a list named {string}` and `When I open the list {string}` steps
(they already exist in `ListSteps`/`ListItemSteps`). If `ctx.SetListItemId` / `GetListItemId` /
`ListIds` signatures differ, match the real ones in `ScenarioContextHolder.cs`.

- [ ] **Step 6: Build the SPA (integration harness serves `ClientApp/build`)**

Run:
```bash
cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run build
```
Expected: build succeeds (new testids land in `build/`).

- [ ] **Step 7: Run the integration tests**

Run (Docker must be running — Testcontainers):
```bash
cd C:/Repositories/frigorino && dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~MediaItem"
```
Expected: PASS. If the API-level upload helper could not be implemented (Playwright multipart API
unavailable), the UI scenario alone must pass and the `.Api.feature` should be removed.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs \
        Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs \
        Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.Api.feature \
        Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.feature \
        Application/Frigorino.IntegrationTests/Slices/Lists/MediaItemSteps.cs
git commit -m "test(it): image upload + byte-serving integration scenarios"
```

- [ ] **Step 9: Full verification gate**

Run, in order:
```bash
cd C:/Repositories/frigorino/Application/Frigorino.Web/ClientApp && npm run lint && npm run tsc && npm run prettier
cd C:/Repositories/frigorino && dotnet test Application/Frigorino.sln
docker build -f Application/Dockerfile -t frigorino .
```
Expected: lint/tsc/prettier clean; full solution tests pass (unit + integration); docker build
succeeds (confirms no Dockerfile drift — no new project was added, so it should be unchanged).
Capture `${PIPESTATUS[0]}` / read pass-fail lines rather than trusting a piped tail exit code.

- [ ] **Step 10: Commit any formatting fixes**

```bash
cd C:/Repositories/frigorino
git add -A
git commit -m "chore: prettier + verification fixes for image items" || echo "nothing to commit"
```

---

## Done criteria

- All 12 tasks committed on `feat/rich-list-items-2-image-items`.
- `dotnet test Application/Frigorino.sln` green (unit + integration); `docker build` succeeds.
- Manual smoke (optional, via `/dev-up` + Playwright MCP): attach a real photo, see the thumbnail,
  open the lightbox, toggle/reorder/delete the image item, then restore it.
- Merge back into `feat/rich-list-items` (the integration branch) — see
  `project_rich_list_items_integration_branch` memory. Do **not** promote to `stage` until #3/#4 land.
```
