# Replace ImageSharp with Magick.NET Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `SixLabors.ImageSharp` with `Magick.NET` behind the unchanged `IImageProcessor` port, removing the ImageSharp v4 license question.

**Architecture:** The `IImageProcessor` contract and `ProcessedImage` record are unchanged, so no slice/caller/DB touches. One implementation file is rewritten, the DI line is swapped, the tests are ported, and the Docker runtime stage drops chiseled for full Ubuntu + `libgomp1` (Magick.NET's native binary needs OpenMP, absent from chiseled). Encoding policy (2560/480 edges, q82/q75, WebP, 64 MP guard, JPEG/PNG/WebP allowlist) is preserved exactly.

**Tech Stack:** .NET 10, Magick.NET-Q8-x64 14.13.1, xUnit, Docker (multi-stage, `aspnet:10.0-noble`).

**Spec:** `docs/superpowers/specs/2026-06-04-replace-imagesharp-with-magicknet-design.md`

**Sequencing note (why not classic per-behavior TDD):** This is an equivalent-behavior library swap with a pre-existing test suite that already encodes the behavior. The Test project pulls ImageSharp *transitively* through Frigorino.Infrastructure, so removing ImageSharp and renaming the processor class breaks the impl and the tests simultaneously. The discipline here is: **keep every commit boundary compiling and green, port tests in lockstep with the impl.** Task 2 still gets a genuine red→green (ported tests fail to compile against the not-yet-written `MagickImageProcessor`, then pass once it exists). Magick.NET is kept side-by-side with ImageSharp until Task 3 so intermediate commits build.

---

### Task 1: Add the Magick.NET package (side-by-side)

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj:24`

- [ ] **Step 1: Add the package reference** (keep ImageSharp for now)

In `Frigorino.Infrastructure.csproj`, add a line directly after the existing `SixLabors.ImageSharp` reference (line 24), inside the same `<ItemGroup>`:

```xml
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
    <PackageReference Include="Magick.NET-Q8-x64" Version="14.13.1" />
```

- [ ] **Step 2: Restore to regenerate lock files**

Run: `dotnet restore Application/Frigorino.sln`
Expected: succeeds; `Application/Frigorino.Infrastructure/packages.lock.json` (and any transitively-affected lock files) gain `Magick.NET-Q8-x64`. Do NOT pass `--locked-mode` here — a plain restore updates the lock files.

- [ ] **Step 3: Build to confirm nothing broke**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS — both packages coexist, no code references Magick.NET yet.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj Application/**/packages.lock.json
git commit -m "build: add Magick.NET-Q8-x64 alongside ImageSharp"
```

---

### Task 2: Rewrite the processor + DI and port the tests (red → green)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/MagickImageProcessor.cs`
- Delete: `Application/Frigorino.Infrastructure/Services/ImageSharpImageProcessor.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/ImageProcessingDependencyInjection.cs`
- Create: `Application/Frigorino.Test/Infrastructure/MagickImageProcessorTests.cs`
- Delete: `Application/Frigorino.Test/Infrastructure/ImageSharpImageProcessorTests.cs`

- [ ] **Step 1: Write the ported test file**

Delete `ImageSharpImageProcessorTests.cs` and create `MagickImageProcessorTests.cs` with the full contents below. (Same seven behaviors, asserted through the `IImageProcessor` contract; fixtures and output inspection use Magick.NET.)

```csharp
using Frigorino.Infrastructure.Services;
using ImageMagick;

namespace Frigorino.Test.Infrastructure
{
    public class MagickImageProcessorTests
    {
        private static byte[] MakePng(int width, int height)
        {
            using var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
            return image.ToByteArray(MagickFormat.Png);
        }

        [Fact]
        public async Task ProcessAsync_ValidPng_ReturnsWebpRenditions()
        {
            var processor = new MagickImageProcessor();
            using var input = new MemoryStream(MakePng(1200, 900));

            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("image/webp", result.Value.ContentType);
            Assert.NotEmpty(result.Value.FullRes);
            Assert.NotEmpty(result.Value.Thumbnail);
            Assert.Equal(result.Value.FullRes.Length, (int)result.Value.FullResSizeBytes);

            // Both renditions are real WebP images.
            var fullInfo = new MagickImageInfo(result.Value.FullRes);
            var thumbInfo = new MagickImageInfo(result.Value.Thumbnail);
            Assert.Equal(MagickFormat.WebP, fullInfo.Format);
            Assert.True(Math.Max(thumbInfo.Width, thumbInfo.Height) <= 480u);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotUpscaleSmallImage()
        {
            var processor = new MagickImageProcessor();
            using var input = new MemoryStream(MakePng(100, 80));

            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var fullInfo = new MagickImageInfo(result.Value.FullRes);
            Assert.Equal(100u, fullInfo.Width);
            Assert.Equal(80u, fullInfo.Height);
        }

        [Fact]
        public async Task ProcessAsync_GarbageBytes_ReturnsFail()
        {
            var processor = new MagickImageProcessor();
            using var input = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task ProcessAsync_StripsExifMetadata()
        {
            var processor = new MagickImageProcessor();
            byte[] withExif;
            using (var image = new MagickImage(MagickColors.White, 50, 50))
            {
                var exif = new ExifProfile();
                exif.SetValue(ExifTag.Copyright, "secret");
                image.SetProfile(exif);
                withExif = image.ToByteArray(MagickFormat.Png);
            }

            using var input = new MemoryStream(withExif);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using var full = new MagickImage(result.Value.FullRes);
            Assert.Null(full.GetExifProfile());
        }

        [Fact]
        public async Task ProcessAsync_AppliesExifOrientation_BeforeStrippingMetadata()
        {
            var processor = new MagickImageProcessor();
            byte[] rotated;
            // Non-square 20x40 with an EXIF orientation of 6 (rotate 90° CW). JPEG round-trips EXIF
            // reliably. After AutoOrient bakes the rotation into pixels, the output dimensions should
            // be transposed (40x20) — proving rotation was applied before EXIF was stripped.
            using (var image = new MagickImage(MagickColors.White, 20, 40))
            {
                var exif = new ExifProfile();
                exif.SetValue(ExifTag.Orientation, (ushort)6);
                image.SetProfile(exif);
                rotated = image.ToByteArray(MagickFormat.Jpeg);
            }

            using var input = new MemoryStream(rotated);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var fullInfo = new MagickImageInfo(result.Value.FullRes);
            Assert.Equal(40u, fullInfo.Width);
            Assert.Equal(20u, fullInfo.Height);
            using var full = new MagickImage(result.Value.FullRes);
            Assert.Null(full.GetExifProfile());
        }

        [Fact]
        public async Task ProcessAsync_ValidButDisallowedFormat_ReturnsFail()
        {
            var processor = new MagickImageProcessor();
            byte[] gif;
            // A real, decodable GIF — not in the JPEG/PNG/WebP allowlist. Distinct from the garbage
            // test (which trips the decode-throw path); this proves a valid-but-unlisted format is
            // rejected by the allowlist.
            using (var image = new MagickImage(MagickColors.White, 30, 30))
            {
                gif = image.ToByteArray(MagickFormat.Gif);
            }

            using var input = new MemoryStream(gif);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task ProcessAsync_DimensionsExceedCeiling_ReturnsFail()
        {
            var processor = new MagickImageProcessor();
            byte[] oversized;
            // 8001 x 8001 = ~64.0 MP, just over the 64 MP ceiling. The guard reads dimensions from the
            // header (MagickImageInfo) and rejects before the full decode. The ~192 MB construction
            // allocation is transient (disposed before assertion).
            using (var image = new MagickImage(MagickColors.White, 8001, 8001))
            {
                oversized = image.ToByteArray(MagickFormat.Png);
            }

            using var input = new MemoryStream(oversized);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsFailed);
        }
    }
}
```

- [ ] **Step 2: Run the ported tests to verify they fail (red)**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~MagickImageProcessorTests"`
Expected: FAIL — compile error, `MagickImageProcessor` does not exist yet.

- [ ] **Step 3: Write the Magick.NET processor**

Delete `ImageSharpImageProcessor.cs` and create `MagickImageProcessor.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Interfaces;
using ImageMagick;

namespace Frigorino.Infrastructure.Services
{
    // Magick.NET-backed IImageProcessor. Encoding policy (sizes/quality/format) lives here as
    // Infrastructure constants — it is rendering policy, not an aggregate invariant. Stateless →
    // safe as a singleton.
    public sealed class MagickImageProcessor : IImageProcessor
    {
        private const int FullResMaxEdge = 2560;
        private const int ThumbnailMaxEdge = 480;
        private const int FullResQuality = 82;
        private const int ThumbnailQuality = 75;
        private const string WebpContentType = "image/webp";

        // Decode-bomb guard. The upstream 25 MB byte cap bounds the COMPRESSED payload, but a tiny,
        // highly-compressed image can still decode into an enormous pixel buffer (DoS). We read the
        // dimensions from the header (MagickImageInfo, no full decode) and reject before constructing
        // the MagickImage. 64 MP comfortably covers 48 MP+ phone cameras while blocking absurd bombs.
        private const long MaxDecodedPixels = 64_000_000;

        // Only these decoders are accepted — shrinks the decode attack surface and avoids surprises
        // (e.g. animated GIF). ImageMagick normalizes JPEG to MagickFormat.Jpeg, but Jpg is included
        // defensively.
        private static readonly HashSet<MagickFormat> AllowedInputFormats =
            new() { MagickFormat.Jpeg, MagickFormat.Jpg, MagickFormat.Png, MagickFormat.WebP };

        public async Task<Result<ProcessedImage>> ProcessAsync(Stream input, CancellationToken ct)
        {
            // Buffer so we can both detect the format/dimensions and decode from the start.
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            try
            {
                // Header-only read: format + declared pixel dimensions without allocating the decoded
                // buffer, so a compressed bomb is rejected before the full decode materializes it.
                var info = new MagickImageInfo(buffer);
                if (!AllowedInputFormats.Contains(info.Format))
                {
                    return Result.Fail(new Error("Unsupported image format.")
                        .WithMetadata("Property", "file"));
                }

                if ((long)info.Width * info.Height > MaxDecodedPixels)
                {
                    return Result.Fail(new Error("Image dimensions exceed the allowed limit.")
                        .WithMetadata("Property", "file"));
                }

                buffer.Position = 0;
                using var image = new MagickImage(buffer);

                // AutoOrient bakes EXIF rotation into pixels (so stripping EXIF can't desync it),
                // then Strip() removes ALL embedded profiles (EXIF/IPTC/XMP/ICC) in one call.
                image.AutoOrient();
                image.Strip();

                var fullRes = EncodeRendition(image, FullResMaxEdge, FullResQuality);
                var thumbnail = EncodeRendition(image, ThumbnailMaxEdge, ThumbnailQuality);

                return Result.Ok(new ProcessedImage(
                    fullRes, thumbnail, WebpContentType, fullRes.LongLength));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result.Fail(new Error("Could not decode the uploaded image.")
                    .WithMetadata("Property", "file"));
            }
        }

        // Encode to WebP into a byte[]. The '>' geometry modifier (Greater) resizes only when the
        // image is larger than the bound — never upscales — and preserves aspect ratio (ImageSharp's
        // ResizeMode.Max + no-upscale equivalent). Clone() so neither rendition mutates the shared
        // decoded image, so it is safe to call twice (full-res then thumbnail).
        private static byte[] EncodeRendition(IMagickImage image, int maxEdge, int quality)
        {
            using var clone = image.Clone();
            clone.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge) { Greater = true });
            clone.Quality = (uint)quality;
            return clone.ToByteArray(MagickFormat.WebP);
        }
    }
}
```

- [ ] **Step 4: Swap the DI registration**

Replace the body of `ImageProcessingDependencyInjection.cs` with:

```csharp
using Frigorino.Domain.Interfaces;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class ImageProcessingDependencyInjection
    {
        // Magick.NET processor is stateless → singleton. Swap the implementation here if the library
        // is ever replaced (the IImageProcessor port keeps callers unchanged). ResourceLimits.Thread
        // is global to ImageMagick; pin to 1 so OpenMP doesn't fan threads per request under Railway's
        // constrained CPU.
        public static IServiceCollection AddImageProcessing(this IServiceCollection services)
        {
            ResourceLimits.Thread = 1;
            services.AddSingleton<IImageProcessor, MagickImageProcessor>();
            return services;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass (green)**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~MagickImageProcessorTests"`
Expected: PASS — all 7 tests green.

- [ ] **Step 6: Run the full solution tests**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS — Test + IntegrationTests green (the integration `MediaItemSteps` exercise the unchanged `IImageProcessor` contract).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services Application/Frigorino.Test/Infrastructure
git commit -m "feat: replace ImageSharp processor with Magick.NET"
```

---

### Task 3: Remove the ImageSharp package

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`

- [ ] **Step 1: Delete the ImageSharp reference**

Remove this line from `Frigorino.Infrastructure.csproj`:

```xml
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
```

- [ ] **Step 2: Verify no ImageSharp references remain**

Run: `grep -rn "SixLabors\|ImageSharp" Application --include=*.cs`
Expected: no matches (all production + test code now uses Magick.NET).

- [ ] **Step 3: Restore to regenerate lock files**

Run: `dotnet restore Application/Frigorino.sln`
Expected: succeeds; `SixLabors.ImageSharp` removed from all `packages.lock.json` files.

- [ ] **Step 4: Build + test to confirm green without ImageSharp**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj Application/**/packages.lock.json
git commit -m "build: drop SixLabors.ImageSharp dependency"
```

---

### Task 4: Update the Dockerfile runtime stage

**Files:**
- Modify: `Application/Dockerfile:1-4`

- [ ] **Step 1: Swap the runtime base, add libgomp1, pin non-root user**

Replace the current runtime stage header (lines 1-4):

```dockerfile
# Runtime: distroless chiseled image — minimal CVE surface, runs as non-root ($APP_UID=1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
```

with:

```dockerfile
# Runtime: full Ubuntu (Noble), NOT chiseled. Magick.NET's native binary needs libgomp1 (OpenMP)
# + libstdc++6, which chiseled images don't ship and there's no chiseled variant that adds them.
# We install libgomp1 (libstdc++6 is present in the full image) and pin USER back to non-root,
# because non-chiseled images default to root (chiseled defaulted to $APP_UID=1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgomp1 \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
```

(The `final` stage already `COPY --chown=$APP_UID ...` and inherits this `USER`; leave it unchanged.)

- [ ] **Step 2: Commit**

```bash
git add Application/Dockerfile
git commit -m "build: full Ubuntu runtime + libgomp1 for Magick.NET native lib"
```

---

### Task 5: Verify the container end-to-end

This is the gate that `dotnet test` cannot cover — a missing native lib or wrong RID only surfaces on first decode inside the container. Requires the Docker daemon running (ask the user to start Docker Desktop if `docker build` reports the daemon is unreachable).

- [ ] **Step 1: Build the image**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: PASS — backend publish (linux-x64, with `Magick.Native-Q8-x64` .so), SPA build, final image assembled.

- [ ] **Step 2: Run the container and upload a real image**

The production image runs the real Firebase + Postgres flow (DevAuth is Development-env only), so supply real config via an env file. Create a throwaway `.env.docker` (DO NOT commit) with at least `ConnectionStrings__Database=...` and the `FirebaseSettings__*` keys, then:

Run: `docker run --rm -p 8080:8080 --env-file .env.docker frigorino`

Open `http://localhost:8080`, sign in, create a list, add an **image** list-item by uploading a real JPEG/PNG, and confirm the image + thumbnail render. Watch container logs for any `Unable to load shared library 'Magick.Native-Q8-x64'` or `libgomp.so.1: cannot open shared object file` — either means the native dep wiring is wrong.
Expected: upload succeeds, both renditions display, no native-load errors in logs.

If running the full production image with real config locally is impractical, the realistic fallback is to verify on the **stage** Railway deploy after merge — but note stage is client UAT (do not push probe commits; verify with a genuine upload, then move on).

- [ ] **Step 3: Confirm the verification result**

Record the outcome (image uploaded + rendered, logs clean). If a native-load error appears, the fix is in Task 4 (missing/incorrect native dep) — do not proceed until clean.

---

### Task 6: Remove the resolved tech-debt entry

**Files:**
- Modify: `TECH_DEBT.md` (the ImageSharp entry, currently lines 26-30)

- [ ] **Step 1: Delete the ImageSharp 3.1.x entry**

Remove the entire `## - **ImageSharp pinned to 3.1.x ...**` block (the heading plus its `Where` / `Why deferred` / `Plan` / `Risk if left` bullets). The work it tracked is now done.

- [ ] **Step 2: Commit**

```bash
git add TECH_DEBT.md
git commit -m "docs: drop resolved ImageSharp-pin tech-debt entry"
```

---

## Notes for the implementer

- **Magick.NET v14 uses `uint`** for `Width`/`Height`/`Quality` (changed from `int` in v13). The code above casts accordingly; assertions use `u` suffixes.
- **`MagickGeometry { Greater = true }`** is the `>` modifier — resize only if larger, never upscale, aspect preserved. It is a no-op for images already within bounds, so no separate size guard is needed (the `DoesNotUpscaleSmallImage` test verifies this).
- **`image.Strip()`** removes all embedded profiles (EXIF/IPTC/XMP/ICC) in one call — replaces the four manual profile-nulls.
- **Do not** commit a `sixlabors.lic` or any license key — they are no longer relevant; ImageMagick (Apache-2.0) needs no key.
- **Lock files:** the repo's Docker restore uses `--locked-mode`, so committed `packages.lock.json` files MUST be current after every package change (Tasks 1 and 3 both restore without `--locked-mode` to refresh them).
