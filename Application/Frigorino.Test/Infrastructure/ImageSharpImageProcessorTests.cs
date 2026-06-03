using FluentResults;
using Frigorino.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
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
            Assert.Equal("WEBP", Image.DetectFormat(result.Value.FullRes).Name, ignoreCase: true);
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

        [Fact]
        public async Task ProcessAsync_AppliesExifOrientation_BeforeStrippingMetadata()
        {
            var processor = new ImageSharpImageProcessor();
            byte[] rotated;
            // Non-square 20x40 with an EXIF orientation of 6 (rotate 90° CW). JPEG round-trips EXIF
            // reliably. After AutoOrient bakes the rotation into pixels, the output dimensions should
            // be transposed (40x20) — proving rotation was applied before EXIF was stripped.
            using (var image = new Image<Rgba32>(20, 40))
            {
                image.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
                image.Metadata.ExifProfile.SetValue(
                    SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, (ushort)6);
                using var ms = new MemoryStream();
                image.Save(ms, new JpegEncoder());
                rotated = ms.ToArray();
            }

            using var input = new MemoryStream(rotated);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using var full = Image.Load(result.Value.FullRes);
            Assert.Equal(40, full.Width);
            Assert.Equal(20, full.Height);
            Assert.Null(full.Metadata.ExifProfile);
        }

        [Fact]
        public async Task ProcessAsync_ValidButDisallowedFormat_ReturnsFail()
        {
            var processor = new ImageSharpImageProcessor();
            byte[] gif;
            // A real, decodable GIF — not in the JPEG/PNG/WebP allowlist. Distinct from the garbage
            // test (which trips the null-format path); this proves a valid-but-unlisted format is
            // rejected by the allowlist.
            using (var image = new Image<Rgba32>(30, 30))
            {
                using var ms = new MemoryStream();
                image.Save(ms, new GifEncoder());
                gif = ms.ToArray();
            }

            using var input = new MemoryStream(gif);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task ProcessAsync_DimensionsExceedCeiling_ReturnsFail()
        {
            var processor = new ImageSharpImageProcessor();
            byte[] oversized;
            // 8001 x 8001 = ~64.0 MP, just over the 64 MP ceiling. Use L8 (1 byte/pixel) to keep the
            // construction allocation ~64 MB instead of 256 MB (Rgba32). The guard reads dimensions
            // from the header (Image.Identify) and rejects before the full decode.
            using (var image = new Image<L8>(8001, 8001))
            {
                using var ms = new MemoryStream();
                image.Save(ms, new PngEncoder());
                oversized = ms.ToArray();
            }

            using var input = new MemoryStream(oversized);
            var result = await processor.ProcessAsync(input, CancellationToken.None);

            Assert.True(result.IsFailed);
        }
    }
}
