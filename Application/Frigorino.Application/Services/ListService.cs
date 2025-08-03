using Frigorino.Application.Extensions;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Application.Services
{
    public class ListService : IListService
    {
        private readonly ApplicationDbContext _dbContext;

        public ListService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<ListDto>> GetAllLists(int householdId, string userId)
        {
            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var lists = await _dbContext.Lists
                .Where(l => l.HouseholdId == householdId && l.IsActive)
                .Include(l => l.CreatedByUser)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return lists.Select(l => l.ToDto());
        }

        public async Task<ListDto?> GetListAsync(int listId, string userId)
        {
            var list = await _dbContext.Lists
                .Include(l => l.CreatedByUser)
                .Include(l => l.Household)
                .FirstOrDefaultAsync(l => l.Id == listId && l.IsActive);

            if (list == null)
            {
                return null;
            }

            // Check if user has access to the household this list belongs to
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == list.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                return null;
            }

            return list.ToDto();
        }

        public async Task<ListDto> CreateListAsync(int householdId, CreateListRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("List name is required.");
            }

            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var list = request.ToEntity(householdId, userId);
            _dbContext.Lists.Add(list);
            await _dbContext.SaveChangesAsync();

            // Return the created list with user information
            var createdList = await GetListAsync(list.Id, userId);
            return createdList!;
        }

        public async Task<ListDto?> UpdateListAsync(int listId, UpdateListRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("List name is required.");
            }

            var list = await _dbContext.Lists
                .Include(l => l.Household)
                .FirstOrDefaultAsync(l => l.Id == listId && l.IsActive);

            if (list == null)
            {
                return null;
            }

            // Check if user has access to the household and permission to edit
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == list.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            // Check if user is the creator of the list or has admin/owner role
            if (list.CreatedByUserId != userId && userAccess.Role < HouseholdRole.Admin)
            {
                throw new UnauthorizedAccessException("You don't have permission to edit this list.");
            }

            list.UpdateFromRequest(request);
            await _dbContext.SaveChangesAsync();

            return await GetListAsync(listId, userId);
        }

        public async Task<bool> DeleteListAsync(int listId, string userId)
        {
            var list = await _dbContext.Lists
                .Include(l => l.Household)
                .FirstOrDefaultAsync(l => l.Id == listId && l.IsActive);

            if (list == null)
            {
                return false;
            }

            // Check if user has access to the household and permission to delete
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == list.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            // Check if user is the creator of the list or has admin/owner role
            if (list.CreatedByUserId != userId && userAccess.Role < HouseholdRole.Admin)
            {
                throw new UnauthorizedAccessException("You don't have permission to delete this list.");
            }

            // Soft delete
            list.IsActive = false;
            list.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return true;
        }
    }
}
