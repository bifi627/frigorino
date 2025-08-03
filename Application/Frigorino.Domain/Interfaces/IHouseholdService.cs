using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IHouseholdService
    {
        // Household management
        Task<IEnumerable<HouseholdDto>> GetUserHouseholdsAsync(string userId);
        Task<HouseholdDto?> GetHouseholdAsync(int id, string userId);
        Task<HouseholdDto> CreateHouseholdAsync(CreateHouseholdRequest request, string userId);
        Task<HouseholdDto?> UpdateHouseholdAsync(int id, UpdateHouseholdRequest request, string userId);
        Task<bool> DeleteHouseholdAsync(int id, string userId);

        // Member management
        Task<IEnumerable<HouseholdMemberDto>> GetHouseholdMembersAsync(int householdId, string userId);
        Task<HouseholdMemberDto?> AddMemberAsync(int householdId, AddMemberRequest request, string userId);
        Task<HouseholdMemberDto?> UpdateMemberRoleAsync(int householdId, string targetUserId, UpdateMemberRoleRequest request, string userId);
        Task<bool> RemoveMemberAsync(int householdId, string targetUserId, string userId);
        Task<bool> LeaveHouseholdAsync(int householdId, string userId);
    }
}