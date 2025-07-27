using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IHouseholdService
    {
        Task<IEnumerable<HouseholdDto>> GetUserHouseholdsAsync(string userId);
        Task<HouseholdDto?> GetHouseholdAsync(int id, string userId);
        Task<HouseholdDto> CreateHouseholdAsync(CreateHouseholdRequest request, string userId);
        Task<HouseholdDto?> UpdateHouseholdAsync(int id, UpdateHouseholdRequest request, string userId);
        Task<bool> DeleteHouseholdAsync(int id, string userId);
    }
}