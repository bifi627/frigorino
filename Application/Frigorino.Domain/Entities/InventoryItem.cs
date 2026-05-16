namespace Frigorino.Domain.Entities
{
    public class InventoryItem
    {
        // Length constants for the Inventory aggregate's validation. TextMaxLength matches the
        // existing DB column width (kept smaller than ListItem's 500 to avoid a needless EF
        // migration). QuantityMaxLength is aggregate-only — the Quantity column has no DB-level
        // length constraint, the aggregate validation is the single gate.
        // Behaviour (Add/Update/Reorder/Compact) lives on the parent Inventory aggregate.
        public const int TextMaxLength = 255;
        public const int QuantityMaxLength = 100;

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
