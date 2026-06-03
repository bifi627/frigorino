using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class ListItem
    {
        // Source of truth for length constraints. Both the List aggregate methods and the
        // EF configuration (ListItemConfiguration) read from this so DB and aggregate agree.
        public const int TextMaxLength = 500;
        public const int CommentMaxLength = 500;

        public int Id { get; set; }
        public int ListId { get; set; }
        public string Text { get; set; } = string.Empty;

        // Optional free-text hint ("the blue one", "ask the butcher"). Distinct from Text:
        // the name stays clean/parseable, the comment stays human prose. Never routed by
        // ItemTextRouter. null = no comment.
        public string? Comment { get; set; }

        // Structured quantity: both columns set together, or both null (no quantity).
        // The both-or-null invariant is enforced by the List aggregate.
        public decimal? QuantityValue { get; set; }
        public QuantityUnit? QuantityUnit { get; set; }

        public bool Status { get; set; } = false; // false = unchecked, true = checked
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public List List { get; set; } = null!;
    }
}
