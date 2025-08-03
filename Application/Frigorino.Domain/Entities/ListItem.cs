namespace Frigorino.Domain.Entities
{
    public class ListItem
    {
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
