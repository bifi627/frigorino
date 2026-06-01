using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class InventoryItem
    {
        // Length constant for the Inventory aggregate's validation. TextMaxLength matches the
        // existing DB column width (kept smaller than ListItem's 500 to avoid a needless EF
        // migration).
        // Behaviour (Add/Update/Reorder/Compact) lives on the parent Inventory aggregate.
        public const int TextMaxLength = 255;

        public int Id { get; set; }
        public int InventoryId { get; set; }
        public string Text { get; set; } = string.Empty;

        // Structured quantity, persisted as two flat nullable columns (mirrors ListItem). Both are
        // set together or both null (the "no quantity" state); the Inventory aggregate enforces it.
        public decimal? QuantityValue { get; set; }
        public QuantityUnit? QuantityUnit { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Inventory Inventory { get; set; } = null!;
    }
}
