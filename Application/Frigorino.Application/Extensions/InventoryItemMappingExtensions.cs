using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Application.Extensions
{
    public static class InventoryItemMappingExtensions
    {
        public static InventoryItemDto ToDto(this InventoryItem inventoryItem)
        {
            return new InventoryItemDto
            {
                Id = inventoryItem.Id,
                InventoryId = inventoryItem.InventoryId,
                Text = inventoryItem.Text,
                Quantity = inventoryItem.Quantity,
                ExpiryDate = inventoryItem.ExpiryDate,
                SortOrder = inventoryItem.SortOrder,
                CreatedAt = inventoryItem.CreatedAt,
                UpdatedAt = inventoryItem.UpdatedAt,
                IsExpiring = inventoryItem.ExpiryDate.HasValue && inventoryItem.ExpiryDate.Value <= DateTime.UtcNow.AddDays(7)
            };
        }

        public static InventoryItem ToEntity(this CreateInventoryItemRequest request, int inventoryId, string userId, int sortOrder)
        {
            return new InventoryItem
            {
                Text = request.Text.Trim(),
                Quantity = request.Quantity?.Trim(),
                ExpiryDate = request.ExpiryDate,
                InventoryId = inventoryId,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static void UpdateFromRequest(this InventoryItem inventoryItem, UpdateInventoryItemRequest request)
        {
            inventoryItem.Text = request.Text.Trim();
            inventoryItem.Quantity = request.Quantity?.Trim();
            inventoryItem.ExpiryDate = request.ExpiryDate;
            inventoryItem.UpdatedAt = DateTime.UtcNow;
        }
    }
}
