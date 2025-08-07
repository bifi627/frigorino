namespace Frigorino.Domain.DTOs
{
    public class InventoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int HouseholdId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserDto CreatedByUser { get; set; } = null!;
        public int TotalItems { get; set; }
        public int ExpiringItems { get; set; }
    }

    public class CreateInventoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateInventoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class InventoryItemDto
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsExpiring { get; set; }
    }

    public class CreateInventoryItemRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class UpdateInventoryItemRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
