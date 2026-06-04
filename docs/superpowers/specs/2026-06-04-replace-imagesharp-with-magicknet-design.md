# Replace ImageSharp with Magick.NET

**Date:** 2026-06-04
**Status:** Approved (design)
**Resolves:** `TECH_DEBT.md` lines 26–30 (ImageSharp pinned to 3.1.x to avoid the v4 license gate)

## Problem

The image-processing for rich-list-item image items uses `SixLabors.ImageSharp`, pinned to `3.1.12` — the last Apache-2.0 line. ImageSharp 4.x adds a build-time license gate with no Apache-2.0 bypass flag, so the deferred upgrade required threading a (free, under-$1M) `SixLaborsLicenseKey` through every build environment. The decision instead is to **move off ImageSharp entirely** to avoid the license question, switching to **Magick.NET** (ImageMagick wrapper, Apache-2.0, no key).

## Key decision driver: native dependencies vs. the chiseled image

ImageSharp's standout property is being 100% managed code with **zero native dependencies**, which is why it slotted cleanly into the distroless/chiseled runtime image (`aspnet:10.0-noble-chiseled`, prized for minimal CVE surface — see `Dockerfile` line 1).

**Every alternative — Magick.NET, SkiaSharp, NetVips — reintroduces a native binary** into a chiseled image that lacks the libraries it needs. Magick.NET's native binary requires `libgomp1` (OpenMP) and `libstdc++6`, neither present in chiseled images. There is no official chiseled variant shipping libgomp.

**Decision (user-approved):** drop the chiseled runtime base for the full `aspnet:10.0-noble` image and `apt-get install libgomp1`. This is the accepted cost of leaving ImageSharp: the license-key ceremony is swapped for a base-image/native-deps ceremony, and the minimal-CVE posture is given up. (SkiaSharp/NetVips would hit the same chiseled wall — ImageSharp's managed-only nature was the only thing keeping us on the minimal image.)

## Scope

The `IImageProcessor` port (`Frigorino.Domain/Interfaces/IImageProcessor.cs`) and the `ProcessedImage` record stay **byte-for-byte identical**. No slice, caller, DTO, or DB change. The encoding *policy* is preserved exactly:

- Full-res max edge 2560, quality 82
- Thumbnail max edge 480, quality 75
- Output WebP (`image/webp`), both renditions
- 64 MP (`MaxDecodedPixels`) decode-bomb ceiling, read header-only before full decode
- Input allowlist: JPEG, PNG, WebP only

Five things change:

1. csproj dependency
2. the one implementation file
3. the DI registration line
4. the Dockerfile runtime stage
5. the tests

## 1. Dependency — `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`

- Remove `<PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />`.
- Add `<PackageReference Include="Magick.NET-Q8-x64" Version="14.x.x" />` — exact-pinned to the latest stable 14.x (per dependency-pinning convention: NuGet uses exact versions). Q8 (8-bit/channel) is correct and leaner than Q16 for phone JPEG/PNG/WebP; `x64` matches the `-r linux-x64` publish RID.
- Regenerate `packages.lock.json` via `dotnet restore` (locked-mode restore runs in the Docker build).

## 2. Processor — rename `ImageSharpImageProcessor.cs` → `MagickImageProcessor.cs`

Same class responsibilities, same constants. API mapping:

| Current (ImageSharp) | Magick.NET |
|---|---|
| `Image.DetectFormatAsync` + allowlist | `new MagickImageInfo(buffer)` → `.Format` checked against allowlist |
| `Image.IdentifyAsync` → pixel-bomb guard | same `MagickImageInfo` → `.Width * .Height` vs `MaxDecodedPixels` (header-only, no full decode) |
| `Image.LoadAsync` | `new MagickImage(buffer)` |
| `image.Mutate(x => x.AutoOrient())` | `image.AutoOrient()` |
| strip EXIF/IPTC/XMP/ICC (4 nulls) | `image.Strip()` (single call removes all profiles) |
| `Clone(x => x.Resize(Max, no-upscale))` | per rendition: `using var clone = image.Clone()` then `clone.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge){ Greater = true })` |
| `WebpEncoder { Quality = q }` | `clone.Format = MagickFormat.WebP; clone.Quality = (uint)q; clone.Write(ms)` |

Details:
- `MagickImageInfo` and `MagickImage` accept a `Stream` / `byte[]`. Keep the existing buffer-to-`MemoryStream` approach so format detection, dimension read, and decode all read from position 0.
- Per-rendition `Clone()` so encoding twice (full-res then thumbnail) never accumulates quality loss and never mutates the shared decoded image — mirrors the existing no-mutate-shared-image safety.
- `Greater = true` on the geometry (the `>` modifier) = shrink-only (never upscale), aspect ratio preserved — the `ResizeMode.Max` + no-upscale equivalent.
- `MagickImage` / `MagickImageInfo` are `IDisposable`; everything stays `using`-scoped.
- Same `try/catch (Exception ex) when (ex is not OperationCanceledException)` → `Result.Fail` with `Property=file` metadata on decode failure; same `Property=file` fails for unsupported-format and oversized-dimension paths.
- **Defense-in-depth:** set `ResourceLimits.Thread = 1` once at startup so ImageMagick's OpenMP does not fan threads per request under Railway's constrained CPU. Set in the DI extension (runs once). Optional but cheap.

## 3. DI — `Application/Frigorino.Infrastructure/Services/ImageProcessingDependencyInjection.cs`

- Swap the registered implementation: `services.AddSingleton<IImageProcessor, MagickImageProcessor>()`.
- Update the stale `// ImageSharp processor is stateless...` comment.
- Optionally set `ResourceLimits.Thread = 1` here (this extension runs once at startup).

## 4. Dockerfile — `Application/Dockerfile` runtime stage

- Change `FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime` → `FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime`.
- Add the native dep:
  ```dockerfile
  RUN apt-get update \
      && apt-get install -y --no-install-recommends libgomp1 \
      && rm -rf /var/lib/apt/lists/*
  ```
- **Add `USER $APP_UID`** — non-chiseled images default to running as **root** (chiseled defaulted to non-root `$APP_UID`). Explicit `USER` prevents a silent regression to root.
- Update the line-1 comment to reflect the new posture (full Ubuntu + libgomp for Magick.NET native binary; no longer minimal-CVE chiseled).

## 5. Tests — `Application/Frigorino.Test/Infrastructure/ImageSharpImageProcessorTests.cs` → `MagickImageProcessorTests.cs`

All seven test cases assert through the `IImageProcessor` contract, so behavior is unchanged. Port the ImageSharp helpers to Magick.NET:

- `MakePng(w, h)` — build a blank PNG via `new MagickImage(MagickColors.White, (uint)w, (uint)h)` → `Write(ms, MagickFormat.Png)`.
- Output inspection (`Image.Load`, `DetectFormat`, `Width`/`Height`, `Metadata.ExifProfile`) → `new MagickImageInfo(bytes)` / `new MagickImage(bytes)` → `.Format`, `.Width`, `.Height`, `.GetExifProfile()`.
- The EXIF-orientation-6 JPEG fixture → build an `ExifProfile`, `SetValue(ExifTag.Orientation, (ushort)6)`, `image.SetProfile(profile)`, write JPEG. Assert output dimensions are transposed (40×20 from a 20×40 input) and `GetExifProfile()` is null after processing.
- The disallowed-format fixture stays a real GIF (`MagickFormat.Gif`); the oversized fixture stays an 8001×8001 image just over 64 MP.

This **fully removes ImageSharp from the Test project**. Integration tests (`MediaItemSteps.cs`) are untouched — the contract is unchanged.

Fallback: if porting the EXIF-orientation fixture to Magick.NET proves disproportionately fiddly, the Test project may retain `SixLabors.ImageSharp` *only* as a neutral fixture/inspection tool (test code is not distributed, so it carries no production license obligation). Default is full removal; this fallback is the explicit escape hatch, not the plan.

## 6. Verification

1. `dotnet test Application/Frigorino.sln` — full solution (Test + IntegrationTests in one run).
2. **`docker build -f Application/Dockerfile -t frigorino .`** — non-negotiable: the native-lib-in-container is exactly the failure a green `dotnet test` will not catch.
3. **Run the built container and upload a real image** through the rich-list-item image flow — runtime native-load failures (missing `libgomp1`, wrong RID) survive static checks and only surface on first decode.

## 7. Cleanup

Delete the resolved ImageSharp entry (`TECH_DEBT.md` lines 26–30) as the finishing step.

## Risks

- **Image size / CVE surface** grows (~100MB+, full Ubuntu) — the accepted cost of leaving chiseled.
- **`libgomp1` must be present** or the app crashes on first decode — caught by the docker-run upload test (step 3).
- **WebP output bytes differ** between ImageMagick and ImageSharp — visually equivalent; the behavioral assertions (`≤ 480px` thumbnail, no upscale, WebP format, EXIF stripped) still hold.
- **Root regression** if `USER $APP_UID` is forgotten — explicitly in scope (§4).
