using Frigorino.Application.Extensions;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Application.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _dbContext;

        public InventoryService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<InventoryDto>> GetAllInventories(int householdId, string userId)
        {
            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var inventories = await _dbContext.Inventories
                .Where(i => i.HouseholdId == householdId && i.IsActive)
                .Include(i => i.CreatedByUser)
                .Include(i => i.InventoryItems)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return inventories.Select(i => i.ToDto());
        }

        public async Task<InventoryDto?> GetInventoryAsync(int inventoryId, string userId)
        {
            var inventory = await _dbContext.Inventories
                .Include(i => i.CreatedByUser)
                .Include(i => i.Household)
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.IsActive);

            if (inventory == null)
            {
                return null;
            }

            // Check if user has access to the household this inventory belongs to
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == inventory.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                return null;
            }

            return inventory.ToDto();
        }

        public async Task<InventoryDto> CreateInventoryAsync(int householdId, CreateInventoryRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Inventory name is required.");
            }

            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == householdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var inventory = request.ToEntity(householdId, userId);
            _dbContext.Inventories.Add(inventory);
            await _dbContext.SaveChangesAsync();

            // Return the created inventory with user information
            var createdInventory = await GetInventoryAsync(inventory.Id, userId);
            return createdInventory!;
        }

        public async Task<InventoryDto?> UpdateInventoryAsync(int inventoryId, UpdateInventoryRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Inventory name is required.");
            }

            var inventory = await _dbContext.Inventories
                .Include(i => i.Household)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.IsActive);

            if (inventory == null)
            {
                return null;
            }

            // Check if user has access to the household and permission to edit
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == inventory.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            // Check if user is the creator of the inventory or has admin/owner role
            if (inventory.CreatedByUserId != userId && userAccess.Role < HouseholdRole.Admin)
            {
                throw new UnauthorizedAccessException("You don't have permission to edit this inventory.");
            }

            inventory.UpdateFromRequest(request);
            await _dbContext.SaveChangesAsync();

            return await GetInventoryAsync(inventoryId, userId);
        }

        public async Task<bool> DeleteInventoryAsync(int inventoryId, string userId)
        {
            var inventory = await _dbContext.Inventories
                .Include(i => i.Household)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.IsActive);

            if (inventory == null)
            {
                return false;
            }

            // Check if user has access to the household and permission to delete
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == inventory.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            // Check if user is the creator of the inventory or has admin/owner role
            if (inventory.CreatedByUserId != userId && userAccess.Role < HouseholdRole.Admin)
            {
                throw new UnauthorizedAccessException("You don't have permission to delete this inventory.");
            }

            // Soft delete
            inventory.IsActive = false;
            inventory.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return true;
        }
    }
}
