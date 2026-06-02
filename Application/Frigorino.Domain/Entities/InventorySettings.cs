namespace Frigorino.Domain.Entities
{
    // Household-wide inventory settings. The former notification fields (per-inventory enable +
    // lead-time override) moved to the per-user UserInventoryNotificationSetting aggregate.
    // This entity + its GET/PUT endpoints are intentionally retained as an empty placeholder for
    // future household-wide inventory configuration, so they don't have to be reinvented later.
    public class InventorySettings
    {
        public int InventoryId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Inventory Inventory { get; set; } = null!;

        public static InventorySettings Create(int inventoryId)
        {
            return new InventorySettings { InventoryId = inventoryId };
        }
    }
}
