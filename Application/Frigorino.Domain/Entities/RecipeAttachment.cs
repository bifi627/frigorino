namespace Frigorino.Domain.Entities
{
    // An image attached to a recipe as source material (dish photo, scanned card). One kind of
    // attachment today (image), so no Type discriminator — every row has a generated thumbnail.
    // Ordering, validation, and lifecycle (add/update-caption/delete/restore/reorder) live on the
    // parent Recipe aggregate; this is a plain data holder. Sibling of RecipeLink.
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

        public int Id { get; set; }
        public int RecipeId { get; set; }

        public string StorageKey { get; set; } = string.Empty;       // full-res WebP blob key (required)
        public string? ThumbnailStorageKey { get; set; }             // nullable column, always set for images
        public string ContentType { get; set; } = string.Empty;      // stored output, always image/webp
        public string? OriginalFileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string? Caption { get; set; }

        // Lexicographic ordering key (fractional index), unique per RECIPE among active rows.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
    }
}
