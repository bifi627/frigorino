using Frigorino.Domain.Products;
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class ListItem
    {
        // Source of truth for length constraints. Both the List aggregate methods and the
        // EF configuration (ListItemConfiguration) read from this so DB and aggregate agree.
        public const int TextMaxLength = 500;
        public const int CommentMaxLength = 500;

        // Media-item limits. Source of truth for both AddMediaItem validation and the EF
        // configuration (fresh columns — widths are set here, not retrofitted). Tunable.
        public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB
        public const int OriginalFileNameMaxLength = 255;
        public const int ContentTypeMaxLength = 255;
        public const int StorageKeyMaxLength = 200;

        // Allowlists are arrays (can't be const). HEIC / office formats are deliberately excluded
        // from v1; extend here when sub-feature #2/#3 widens support.
        public static readonly string[] ImageContentTypes =
            ["image/jpeg", "image/png", "image/webp"];
        public static readonly string[] DocumentContentTypes =
            ["application/pdf"];

        public int Id { get; set; }
        public int ListId { get; set; }
        public string Text { get; set; } = string.Empty;

        // Text (default) | Image | Document. Existing rows backfill to Text via the migration default.
        public ListItemType Type { get; set; } = ListItemType.Text;

        // Optional free-text hint ("the blue one", "ask the butcher"). Distinct from Text:
        // the name stays clean/parseable, the comment stays human prose. Never routed by
        // ItemTextRouter. null = no comment.
        public string? Comment { get; set; }

        // Media-item columns. All null for Text items. For media items Text == "" and the optional
        // caption reuses Comment (clean-separation: Text/Comment keep one meaning each).
        public string? StorageKey { get; set; }
        public string? ThumbnailStorageKey { get; set; } // images only
        public string? OriginalFileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSizeBytes { get; set; }

        // Structured quantity: both columns set together, or both null (no quantity).
        // The both-or-null invariant is enforced by the List aggregate.
        public decimal? QuantityValue { get; set; }
        public QuantityUnit? QuantityUnit { get; set; }

        public bool Status { get; set; } = false; // false = unchecked, true = checked
        public int SortOrder { get; set; }

        // Lexicographic ordering key (fractional index). Opaque, server-minted, never shown in UI.
        // Replaces SortOrder; SortOrder is retained as a dead column until the deferred cleanup.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Promotion-to-inventory state (replaces the device-local localStorage batch). All null
        // for items never checked-while-perishable. Pending promotion =
        //   Status && PromotionExpiryHandling != null && PromotionResolvedAt == null.
        // Stamped/cleared exclusively by List aggregate methods (ToggleItemStatus,
        // ApplyPromotionSuggestion, ResolvePromotion).
        public ExpiryHandling? PromotionExpiryHandling { get; set; }
        public DateOnly? PromotionSuggestedExpiry { get; set; }
        public DateTime? PromotionResolvedAt { get; set; }

        // Navigation properties
        public List List { get; set; } = null!;
    }
}
