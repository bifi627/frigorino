using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IListService
    {
        // List management
        Task<IEnumerable<ListDto>> GetAllLists(int householdId, string userId);
        Task<ListDto?> GetListAsync(int listId, string userId);
        Task<ListDto> CreateListAsync(int householdId, CreateListRequest request, string userId);
        Task<ListDto?> UpdateListAsync(int listId, UpdateListRequest request, string userId);
        Task<bool> DeleteListAsync(int listId, string userId);
    }
}
