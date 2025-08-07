namespace Frigorino.Domain.Entities
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Inventory Inventory { get; set; } = null!;
    }
}
