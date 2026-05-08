using Frigorino.Domain.DTOs;

namespace Frigorino.Domain.Interfaces
{
    public interface IHouseholdService
    {
        // Member management
        Task<HouseholdMemberDto?> UpdateMemberRoleAsync(int householdId, string targetUserId, UpdateMemberRoleRequest request, string userId);
    }
}
