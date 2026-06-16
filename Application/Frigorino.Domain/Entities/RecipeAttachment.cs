namespace Frigorino.Domain.Entities
{
    // Discriminates the two kinds of recipe attachment. Stored as int (Image=0, Document=1);
    // serialized as its string name on the wire via the global JsonStringEnumConverter.
    public enum AttachmentType
    {
        Image = 0,
        Document = 1,
    }

    // An attachment on a recipe as source material — an image (dish photo, scanned card) or a
    // document (PDF). The Type discriminator distinguishes them: images carry a generated thumbnail,
    // documents have none. Ordering, validation, and lifecycle (add/update-caption/delete/restore/
    // reorder) live on the parent Recipe aggregate; this is a plain data holder. Sibling of RecipeLink.
    public class RecipeAttachment
    {
        // Media limits — own source of truth (mirrors ListItem's media values; no cross-aggregate coupling).
        public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB
        public const int StorageKeyMaxLength = 200;
        public const int ContentTypeMaxLength = 255;
        public const int OriginalFileNameMaxLength = 255;
        public const int CaptionMaxLength = 255;

        // Accepted *input* content types (the slice pre-filter). Stored output is always image/webp.
        public static readonly string[] ImageContentTypes =
            ["image/jpeg", "image/png", "image/webp"];

        // Accepted document content types (stored as-is — no re-encoding, no thumbnail).
        public static readonly string[] DocumentContentTypes = ["application/pdf"];

        public int Id { get; set; }
        public int RecipeId { get; set; }

        public string StorageKey { get; set; } = string.Empty;       // full-res WebP blob key (required)
        public string? ThumbnailStorageKey { get; set; }             // nullable column, always set for images
        public string ContentType { get; set; } = string.Empty;      // stored output, always image/webp
        public string? OriginalFileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string? Caption { get; set; }
        public AttachmentType Type { get; set; } = AttachmentType.Image;

        // Lexicographic ordering key (fractional index), unique per RECIPE among active rows.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
    }
}
