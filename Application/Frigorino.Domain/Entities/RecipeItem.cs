using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class RecipeItem
    {
        // Entry is List-style (free-text "250g whole wheat flour…"), so Text matches ListItem's
        // 500 cap, not InventoryItem's 255. Comment matches ListItem.Comment. Behaviour lives on
        // the parent Recipe aggregate.
        public const int TextMaxLength = 500;
        public const int CommentMaxLength = 500;

        public int Id { get; set; }
        public int RecipeId { get; set; }
        public int SectionId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Comment { get; set; }

        // Structured quantity, two flat nullable columns (mirrors ListItem/InventoryItem). Both set
        // together or both null; the Recipe aggregate enforces it.
        public decimal? QuantityValue { get; set; }
        public QuantityUnit? QuantityUnit { get; set; }

        // Lexicographic ordering key (fractional index), unique per SECTION. No status split.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
        public RecipeSection Section { get; set; } = null!;
    }
}
