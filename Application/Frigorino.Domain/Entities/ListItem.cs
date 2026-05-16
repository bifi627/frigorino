namespace Frigorino.Domain.Entities
{
    public class ListItem
    {
        // Source of truth for length constraints. Both the List aggregate methods and the
        // EF configuration (ListItemConfiguration) read from these so DB and aggregate agree.
        public const int TextMaxLength = 500;
        public const int QuantityMaxLength = 100;

        public int Id { get; set; }
        public int ListId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public bool Status { get; set; } = false; // false = unchecked, true = checked
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public List List { get; set; } = null!;
    }
}
