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
