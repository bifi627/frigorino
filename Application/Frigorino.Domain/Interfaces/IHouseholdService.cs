using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IHouseholdService
    {
        // Household management
        Task<HouseholdDto?> UpdateHouseholdAsync(int id, UpdateHouseholdRequest request, string userId);

        // Member management
        Task<IEnumerable<HouseholdMemberDto>> GetHouseholdMembersAsync(int householdId, string userId);
        Task<HouseholdMemberDto?> AddMemberAsync(int householdId, AddMemberRequest request, string userId);
        Task<HouseholdMemberDto?> UpdateMemberRoleAsync(int householdId, string targetUserId, UpdateMemberRoleRequest request, string userId);
        Task<bool> RemoveMemberAsync(int householdId, string targetUserId, string userId);
        Task<bool> LeaveHouseholdAsync(int householdId, string userId);
    }
}