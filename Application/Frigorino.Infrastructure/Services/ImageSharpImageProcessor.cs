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
