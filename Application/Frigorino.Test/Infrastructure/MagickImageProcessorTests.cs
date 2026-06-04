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
                // ImageMagick only writes an orientation tag when an EXIF profile is attached, and it
                // syncs that tag from image.Orientation on write — so we need BOTH: a profile to carry
                // the tag, and the property set to RightTop (== EXIF orientation 6, rotate 90° CW).
                var exif = new ExifProfile();
                exif.SetValue(ExifTag.Orientation, (ushort)6);
                image.SetProfile(exif);
                image.Orientation = OrientationType.RightTop;
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
