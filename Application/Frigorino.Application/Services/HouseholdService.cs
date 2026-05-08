using Frigorino.Application.Extensions;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Application.Services
{
    public class HouseholdService : IHouseholdService
    {
        private readonly ApplicationDbContext _dbContext;

        public HouseholdService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        #region Member Management

        public async Task<HouseholdMemberDto?> UpdateMemberRoleAsync(int householdId, string targetUserId, UpdateMemberRoleRequest request, string userId)
        {
            // Check if current user has admin/owner permissions
            var currentUserRole = await GetUserRoleInHouseholdAsync(householdId, userId);
            if (currentUserRole == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            if (currentUserRole == HouseholdRole.Member)
            {
                throw new UnauthorizedAccessException("You don't have permission to update member roles in this household.");
            }

            // Find target membership
            var targetMembership = await _dbContext.UserHouseholds
                .Include(uh => uh.User)
                .FirstOrDefaultAsync(uh => uh.UserId == targetUserId && uh.HouseholdId == householdId && uh.IsActive);

            if (targetMembership == null)
            {
                return null;
            }

            // Prevent users from changing their own role or demoting themselves if they're the only owner
            if (targetUserId == userId)
            {
                if (targetMembership.Role == HouseholdRole.Owner && request.Role != HouseholdRole.Owner)
                {
                    var ownerCount = await _dbContext.UserHouseholds
                        .CountAsync(uh => uh.HouseholdId == householdId && uh.Role == HouseholdRole.Owner && uh.IsActive);

                    if (ownerCount <= 1)
                    {
                        throw new InvalidOperationException("Cannot remove the last owner from a household.");
                    }
                }
            }

            // Only owners can change other owners' roles
            if (targetMembership.Role == HouseholdRole.Owner && currentUserRole != HouseholdRole.Owner)
            {
                throw new UnauthorizedAccessException("Only owners can change other owners' roles.");
            }

            // Update role
            targetMembership.Role = request.Role;
            await _dbContext.SaveChangesAsync();

            return targetMembership.ToMemberDto();
        }

        public async Task<bool> RemoveMemberAsync(int householdId, string targetUserId, string userId)
        {
            // Check if current user has admin/owner permissions
            var currentUserRole = await GetUserRoleInHouseholdAsync(householdId, userId);
            if (currentUserRole == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            if (currentUserRole == HouseholdRole.Member && targetUserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to remove members from this household.");
            }

            // Find target membership
            var targetMembership = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == targetUserId && uh.HouseholdId == householdId && uh.IsActive);

            if (targetMembership == null)
            {
                return false;
            }

            // Prevent removing the last owner
            if (targetMembership.Role == HouseholdRole.Owner)
            {
                var ownerCount = await _dbContext.UserHouseholds
                    .CountAsync(uh => uh.HouseholdId == householdId && uh.Role == HouseholdRole.Owner && uh.IsActive);

                if (ownerCount <= 1)
                {
                    throw new InvalidOperationException("Cannot remove the last owner from a household.");
                }
            }

            // Remove member (soft delete)
            targetMembership.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> LeaveHouseholdAsync(int householdId, string userId)
        {
            return await RemoveMemberAsync(householdId, userId, userId);
        }

        #endregion

        #region Private Helper Methods

        private async Task<HouseholdRole?> GetUserRoleInHouseholdAsync(int householdId, string userId)
        {
            return await _dbContext.UserHouseholds
                .Where(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive)
                .Select(uh => uh.Role)
                .FirstOrDefaultAsync();
        }

        #endregion
    }
}
