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

        public async Task<IEnumerable<HouseholdDto>> GetUserHouseholdsAsync(string userId)
        {
            var userHouseholds = await _dbContext.UserHouseholds
                .Where(uh => uh.UserId == userId && uh.IsActive)
                .Include(uh => uh.Household)
                .ThenInclude(h => h.CreatedByUser)
                .Include(uh => uh.Household)
                .ThenInclude(h => h.UserHouseholds.Where(x => x.IsActive))
                .ThenInclude(uh => uh.User)
                .ToListAsync();

            return userHouseholds.Select(uh => uh.ToDto());
        }

        public async Task<HouseholdDto?> GetHouseholdAsync(int id, string userId)
        {
            var userHousehold = await _dbContext.UserHouseholds
                .Include(uh => uh.Household)
                .ThenInclude(h => h.CreatedByUser)
                .Include(uh => uh.Household)
                .ThenInclude(h => h.UserHouseholds.Where(x => x.IsActive))
                .ThenInclude(uh => uh.User)
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == id && uh.IsActive);

            return userHousehold?.ToDto();
        }

        public async Task<HouseholdDto> CreateHouseholdAsync(CreateHouseholdRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Household name is required.");
            }

            var household = request.ToEntity(userId);
            _dbContext.Households.Add(household);
            await _dbContext.SaveChangesAsync();

            // Add creator as owner
            var userHousehold = new UserHousehold
            {
                UserId = userId,
                HouseholdId = household.Id,
                Role = HouseholdRole.Owner,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.UserHouseholds.Add(userHousehold);
            await _dbContext.SaveChangesAsync();

            // Return the created household
            var createdHousehold = await GetHouseholdAsync(household.Id, userId);
            return createdHousehold!;
        }

        public async Task<HouseholdDto?> UpdateHouseholdAsync(int id, UpdateHouseholdRequest request, string userId)
        {
            var userHousehold = await _dbContext.UserHouseholds
                .Include(uh => uh.Household)
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == id && uh.IsActive);

            if (userHousehold == null)
            {
                return null;
            }

            // Check permissions (Admin or Owner can update)
            if (userHousehold.Role == HouseholdRole.Member)
            {
                throw new UnauthorizedAccessException("You don't have permission to update this household.");
            }

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Household name is required.");
            }

            // Update household using extension method
            userHousehold.Household.UpdateFromRequest(request);
            await _dbContext.SaveChangesAsync();

            // Return updated household
            return await GetHouseholdAsync(id, userId);
        }

        public async Task<bool> DeleteHouseholdAsync(int id, string userId)
        {
            var userHousehold = await _dbContext.UserHouseholds
                .Include(uh => uh.Household)
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == id && uh.IsActive);

            if (userHousehold == null)
            {
                return false;
            }

            // Check permissions (Only Owner can delete)
            if (userHousehold.Role != HouseholdRole.Owner)
            {
                throw new UnauthorizedAccessException("Only the household owner can delete the household.");
            }

            // Soft delete household and all memberships
            userHousehold.Household.IsActive = false;
            userHousehold.Household.UpdatedAt = DateTime.UtcNow;

            var allMemberships = await _dbContext.UserHouseholds
                .Where(uh => uh.HouseholdId == id)
                .ToListAsync();

            foreach (var membership in allMemberships)
            {
                membership.IsActive = false;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        #region Member Management

        public async Task<IEnumerable<HouseholdMemberDto>> GetHouseholdMembersAsync(int householdId, string userId)
        {
            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var members = await _dbContext.UserHouseholds
                .Where(uh => uh.HouseholdId == householdId && uh.IsActive)
                .Include(uh => uh.User)
                .OrderByDescending(m => m.Role) // Owners first, then Admins, then Members
                .ThenBy(m => m.JoinedAt)
                .ToListAsync();

            return members.Select(uh => uh.ToMemberDto());
        }

        public async Task<HouseholdMemberDto?> AddMemberAsync(int householdId, AddMemberRequest request, string userId)
        {
            // Check if current user has admin/owner permissions
            var currentUserRole = await GetUserRoleInHouseholdAsync(householdId, userId);
            if (currentUserRole == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            if (currentUserRole == HouseholdRole.Member)
            {
                throw new UnauthorizedAccessException("You don't have permission to add members to this household.");
            }

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email is required.");
            }

            var email = request.Email.Trim().ToLowerInvariant();

            // Find user by email
            var targetUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

            if (targetUser == null)
            {
                throw new ArgumentException("User with this email not found.");
            }

            // Check if user is already a member
            var existingMembership = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == targetUser.ExternalId && uh.HouseholdId == householdId);

            if (existingMembership != null)
            {
                if (existingMembership.IsActive)
                {
                    throw new InvalidOperationException("User is already a member of this household.");
                }
                else
                {
                    // Reactivate existing membership
                    existingMembership.IsActive = true;
                    existingMembership.Role = request.Role ?? HouseholdRole.Member;
                    existingMembership.JoinedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Create new membership
                var newMembership = new UserHousehold
                {
                    UserId = targetUser.ExternalId,
                    HouseholdId = householdId,
                    Role = request.Role ?? HouseholdRole.Member,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _dbContext.UserHouseholds.Add(newMembership);
            }

            await _dbContext.SaveChangesAsync();

            // Return the new member info
            return new HouseholdMemberDto
            {
                User = targetUser.ToDto(),
                Role = request.Role ?? HouseholdRole.Member,
                JoinedAt = DateTime.UtcNow
            };
        }

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
