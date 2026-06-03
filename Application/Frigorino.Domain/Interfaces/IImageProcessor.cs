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
