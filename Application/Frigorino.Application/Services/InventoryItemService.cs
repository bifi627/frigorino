using Frigorino.Application.Extensions;
using Frigorino.Application.Utilities;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Application.Services
{
    public class InventoryItemService : IInventoryItemService
    {
        private readonly ApplicationDbContext _dbContext;

        public InventoryItemService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<InventoryItemDto>> GetAllInventoryItems(int inventoryId, string userId)
        {
            // First, check if the inventory exists and user has access
            var inventory = await _dbContext.Inventories
                .Include(i => i.Household)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.IsActive);

            if (inventory == null)
            {
                throw new ArgumentException("Inventory not found.");
            }

            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == inventory.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var inventoryItems = await _dbContext.InventoryItems
                .Where(ii => ii.InventoryId == inventoryId && ii.IsActive)
                .OrderBy(ii => ii.SortOrder)
                .ThenByDescending(ii => ii.CreatedAt)
                .ToListAsync();

            return inventoryItems.Select(ii => ii.ToDto());
        }

        public async Task<InventoryItemDto?> GetInventoryItemAsync(int inventoryItemId, string userId)
        {
            var inventoryItem = await _dbContext.InventoryItems
                .Include(ii => ii.Inventory)
                .ThenInclude(i => i.Household)
                .FirstOrDefaultAsync(ii => ii.Id == inventoryItemId && ii.IsActive);

            if (inventoryItem == null)
            {
                return null;
            }

            // Check if user has access to the household this inventory item belongs to
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == inventoryItem.Inventory.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                return null;
            }

            return inventoryItem.ToDto();
        }

        public async Task<InventoryItemDto> CreateInventoryItemAsync(int inventoryId, CreateInventoryItemRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("Inventory item text is required.");
            }

            // Check if the inventory exists and user has access
            var inventory = await _dbContext.Inventories
                .Include(i => i.Household)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.IsActive);

            if (inventory == null)
            {
                throw new ArgumentException("Inventory not found.");
            }

            // Check if user has access to this household
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == inventory.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            // Calculate sort order for new item (always goes to top of unchecked section)
            // In realistic usage, items are added one at a time, so we use the standard approach
            var existingUncheckedItems = await _dbContext.InventoryItems
                .Where(li => li.InventoryId == inventoryId && li.IsActive)
                .OrderBy(li => li.SortOrder)
                .ToListAsync();

            var sortOrder = UpdateSortOrder(existingUncheckedItems, false, null, null);

            var inventoryItem = request.ToEntity(inventoryId, userId, sortOrder);

            // Calculate sort order using the SortOrderCalculator utility
            var lastSortOrder = await _dbContext.InventoryItems
                .Where(ii => ii.InventoryId == inventoryId && ii.IsActive)
                .Select(ii => ii.SortOrder).ToListAsync();

            inventoryItem.SortOrder = lastSortOrder.DefaultIfEmpty(0).Max() + 1000;

            _dbContext.InventoryItems.Add(inventoryItem);
            await _dbContext.SaveChangesAsync();

            // Return the created inventory item with user information
            var createdInventoryItem = await GetInventoryItemAsync(inventoryItem.Id, userId);
            return createdInventoryItem!;
        }

        public async Task<InventoryItemDto?> UpdateInventoryItemAsync(int inventoryItemId, UpdateInventoryItemRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("Inventory item text is required.");
            }

            var inventoryItem = await _dbContext.InventoryItems
                .Include(ii => ii.Inventory)
                .ThenInclude(i => i.Household)
                .FirstOrDefaultAsync(ii => ii.Id == inventoryItemId && ii.IsActive);

            if (inventoryItem == null)
            {
                return null;
            }

            // Check if user has access to the household and permission to edit
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == inventoryItem.Inventory.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            inventoryItem.UpdateFromRequest(request);
            await _dbContext.SaveChangesAsync();

            return await GetInventoryItemAsync(inventoryItemId, userId);
        }

        public async Task<bool> DeleteInventoryItemAsync(int inventoryItemId, string userId)
        {
            var inventoryItem = await _dbContext.InventoryItems
                .Include(ii => ii.Inventory)
                .ThenInclude(i => i.Household)
                .FirstOrDefaultAsync(ii => ii.Id == inventoryItemId && ii.IsActive);

            if (inventoryItem == null)
            {
                return false;
            }

            // Check if user has access to the household and permission to delete
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == inventoryItem.Inventory.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            // Soft delete
            inventoryItem.IsActive = false;
            inventoryItem.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<InventoryItemDto?> ReorderItemAsync(int inventoryItemId, ReorderItemRequest request, string userId)
        {
            var inventoryItem = await _dbContext.InventoryItems
                .Include(ii => ii.Inventory)
                .ThenInclude(i => i.Household)
                .Include(ii => ii.Inventory).ThenInclude(i => i.InventoryItems)
                .FirstOrDefaultAsync(ii => ii.Id == inventoryItemId && ii.IsActive);

            if (inventoryItem == null)
            {
                return null;
            }

            // Check if user has access to the household and permission to delete
            var userAccess = await _dbContext.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId && uh.HouseholdId == inventoryItem.Inventory.HouseholdId && uh.IsActive);

            if (userAccess == null)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }

            var items = inventoryItem.Inventory.InventoryItems.OrderBy(l => l.SortOrder).ToArray();
            var afterItem = items.FirstOrDefault(item => item.Id == request.AfterId);
            InventoryItem? beforeItem = null;
            if (afterItem is not null)
            {
                beforeItem = items.FirstOrDefault(item => item.SortOrder > afterItem.SortOrder);
            }

            var sortOrder = UpdateSortOrder(inventoryItem.Inventory.InventoryItems, false, afterItem?.SortOrder ?? 0, beforeItem?.SortOrder ?? 0);
            inventoryItem.SortOrder = sortOrder;

            await _dbContext.SaveChangesAsync();

            return inventoryItem.ToDto();
        }

        public static int UpdateSortOrder(IEnumerable<Domain.Entities.InventoryItem> allEntries, bool status, int? after, int? before)
        {
            var existingItems = allEntries.OrderBy(x => x.SortOrder).ToList();
            var first = existingItems.FirstOrDefault()?.SortOrder ?? null;
            var last = existingItems.LastOrDefault()?.SortOrder ?? null;

            var result = SortOrderCalculator.CalculateSortOrder(status, after, before, first, last, out bool needRecalculation);

            return result ?? -1;
        }
    }
}
