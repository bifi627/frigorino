using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IListItemService
    {
        // Basic CRUD operations
        Task<IEnumerable<ListItemDto>> GetItemsByListIdAsync(int listId, string userId);
        Task<ListItemDto?> GetItemAsync(int itemId, string userId);
        Task<ListItemDto> CreateItemAsync(int listId, CreateListItemRequest request, string userId, bool @checked = false);
        Task<ListItemDto?> UpdateItemAsync(int itemId, UpdateListItemRequest request, string userId);
        Task<bool> DeleteItemAsync(int itemId, string userId);

        // Sorting operations
        Task<ListItemDto?> ReorderItemAsync(int itemId, ReorderItemRequest request, string userId);
        Task<ListItemDto?> ToggleItemStatusAsync(int itemId, string userId);

        // Utility operations
        Task<bool> RecalculateFullSortOrder(int listId, string userId);
    }
}
