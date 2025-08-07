using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IInventoryItemService
    {
        // Inventory item management
        Task<IEnumerable<InventoryItemDto>> GetAllInventoryItems(int inventoryId, string userId);
        Task<InventoryItemDto?> GetInventoryItemAsync(int inventoryItemId, string userId);
        Task<InventoryItemDto> CreateInventoryItemAsync(int inventoryId, CreateInventoryItemRequest request, string userId);
        Task<InventoryItemDto?> UpdateInventoryItemAsync(int inventoryItemId, UpdateInventoryItemRequest request, string userId);
        Task<bool> DeleteInventoryItemAsync(int inventoryItemId, string userId);
    }
}
