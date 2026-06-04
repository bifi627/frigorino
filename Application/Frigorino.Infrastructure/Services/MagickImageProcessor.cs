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
        private static byte[] EncodeRendition(MagickImage image, int maxEdge, int quality)
        {
            using var clone = image.Clone();
            clone.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge) { Greater = true });
            clone.Quality = (uint)quality;
            return clone.ToByteArray(MagickFormat.WebP);
        }
    }
}
