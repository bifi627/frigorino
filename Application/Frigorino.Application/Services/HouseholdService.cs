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
    }
}
