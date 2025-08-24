using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IInventoryService
    {
        // Inventory management
        Task<IEnumerable<InventoryDto>> GetAllInventories(int householdId, string userId);
        Task<InventoryDto?> GetInventoryAsync(int inventoryId, string userId);
        Task<InventoryDto> CreateInventoryAsync(int householdId, CreateInventoryRequest request, string userId);
        Task<InventoryDto?> UpdateInventoryAsync(int inventoryId, UpdateInventoryRequest request, string userId);
        Task<bool> DeleteInventoryAsync(int inventoryId, string userId);
        Task<bool> RecalculateFullSortOrder(int inventoryId, string userId, bool isBackgroundJob = false);
    }
}
