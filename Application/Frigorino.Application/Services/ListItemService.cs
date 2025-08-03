using Frigorino.Application.Extensions;
using Frigorino.Application.Utilities;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Application.Services
{
    public class ListItemService : IListItemService
    {
        private readonly ApplicationDbContext _dbContext;

        public ListItemService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<ListItemDto>> GetItemsByListIdAsync(int listId, string userId)
        {
            // First check if user has access to this list
            await ValidateListAccessAsync(listId, userId);

            var items = await _dbContext.ListItems
                .Where(li => li.ListId == listId && li.IsActive)
                .OrderBy(li => li.SortOrder)
                .ToListAsync();

            return items.ToDto();
        }

        public async Task<ListItemDto?> GetItemAsync(int itemId, string userId)
        {
            var item = await _dbContext.ListItems
                .Include(li => li.List)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return null;
            }

            // Check if user has access to the household this list belongs to
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == item.List.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                return null;
            }

            return item.ToDto();
        }

        public async Task<ListItemDto> CreateItemAsync(int listId, CreateListItemRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("Item text is required.");
            }

            // Validate user access to the list
            await ValidateListAccessAsync(listId, userId);

            // Calculate sort order for new item (always goes to top of unchecked section)
            var sortOrder = SortOrderCalculator.GetNewItemSortOrder();

            var item = request.ToEntity(listId, sortOrder);
            _dbContext.ListItems.Add(item);
            await _dbContext.SaveChangesAsync();

            return item.ToDto();
        }

        public async Task<ListItemDto?> UpdateItemAsync(int itemId, UpdateListItemRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("Item text is required.");
            }

            var item = await _dbContext.ListItems
                .Include(li => li.List)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return null;
            }

            // Validate user access
            await ValidateListAccessAsync(item.ListId, userId);

            // If status is changing, update sort order accordingly
            var statusChanged = item.Status != request.Status;
            
            item.UpdateFromRequest(request);

            if (statusChanged)
            {
                item.SortOrder = request.Status 
                    ? SortOrderCalculator.GetCheckedStatusSortOrder()
                    : SortOrderCalculator.GetUncheckedStatusSortOrder();
            }

            await _dbContext.SaveChangesAsync();
            return item.ToDto();
        }

        public async Task<bool> DeleteItemAsync(int itemId, string userId)
        {
            var item = await _dbContext.ListItems
                .Include(li => li.List)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return false;
            }

            // Validate user access
            await ValidateListAccessAsync(item.ListId, userId);

            // Soft delete
            item.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<ListItemDto?> ReorderItemAsync(int itemId, ReorderItemRequest request, string userId)
        {
            var item = await _dbContext.ListItems
                .Include(li => li.List)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return null;
            }

            // Validate user access
            await ValidateListAccessAsync(item.ListId, userId);

            // Get all items in the same list and status group for sort order calculation
            var sameStatusItems = await _dbContext.ListItems
                .Where(li => li.ListId == item.ListId && li.Status == item.Status && li.IsActive && li.Id != itemId)
                .OrderBy(li => li.SortOrder)
                .Select(li => new { li.Id, li.SortOrder })
                .ToListAsync();

            int newSortOrder;

            if (request.AfterItemId == 0)
            {
                // Move to top of section
                newSortOrder = item.Status 
                    ? SortOrderCalculator.GetCheckedStatusSortOrder()
                    : SortOrderCalculator.GetNewItemSortOrder();
            }
            else
            {
                // Find the after and before items
                var afterItem = sameStatusItems.FirstOrDefault(i => i.Id == request.AfterItemId);
                if (afterItem == null)
                {
                    throw new ArgumentException("Invalid AfterItemId");
                }

                var afterIndex = sameStatusItems.IndexOf(afterItem);
                var beforeItem = afterIndex + 1 < sameStatusItems.Count ? sameStatusItems[afterIndex + 1] : null;

                newSortOrder = SortOrderCalculator.CalculateReorderSortOrder(
                    afterItem.SortOrder, 
                    beforeItem?.SortOrder, 
                    item.Status);
            }

            item.SortOrder = newSortOrder;
            await _dbContext.SaveChangesAsync();

            // Check if compaction is needed
            var allSortOrders = await _dbContext.ListItems
                .Where(li => li.ListId == item.ListId && li.IsActive)
                .Select(li => li.SortOrder)
                .ToListAsync();

            if (SortOrderCalculator.NeedsCompaction(allSortOrders))
            {
                // Background task could be triggered here for compaction
                // For now, we'll leave it as manual operation
            }

            return item.ToDto();
        }

        public async Task<ListItemDto?> ToggleItemStatusAsync(int itemId, string userId)
        {
            var item = await _dbContext.ListItems
                .Include(li => li.List)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return null;
            }

            // Validate user access
            await ValidateListAccessAsync(item.ListId, userId);

            // Toggle status and update sort order
            item.Status = !item.Status;
            item.SortOrder = item.Status 
                ? SortOrderCalculator.GetCheckedStatusSortOrder()
                : SortOrderCalculator.GetUncheckedStatusSortOrder();

            await _dbContext.SaveChangesAsync();
            return item.ToDto();
        }

        public async Task<bool> CompactListSortOrdersAsync(int listId, string userId)
        {
            // Validate user access
            await ValidateListAccessAsync(listId, userId);

            var items = await _dbContext.ListItems
                .Where(li => li.ListId == listId && li.IsActive)
                .OrderBy(li => li.Status)
                .ThenBy(li => li.SortOrder)
                .ToListAsync();

            if (items.Count == 0)
            {
                return false;
            }

            var uncheckedItems = items.Where(i => !i.Status).ToList();
            var checkedItems = items.Where(i => i.Status).ToList();

            var (uncheckedOrders, checkedOrders) = SortOrderCalculator.GenerateCompactedSortOrders(
                uncheckedItems.Count, 
                checkedItems.Count);

            // Update sort orders
            for (int i = 0; i < uncheckedItems.Count; i++)
            {
                uncheckedItems[i].SortOrder = uncheckedOrders[i];
            }

            for (int i = 0; i < checkedItems.Count; i++)
            {
                checkedItems[i].SortOrder = checkedOrders[i];
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        #region Private Helper Methods

        private async Task ValidateListAccessAsync(int listId, string userId)
        {
            var list = await _dbContext.Lists
                .FirstOrDefaultAsync(l => l.Id == listId && l.IsActive);

            if (list == null)
            {
                throw new ArgumentException("List not found.");
            }

            // Check if user has access to the household this list belongs to
            var userAccess = await _dbContext.UserHouseholds
                .AnyAsync(uh => uh.UserId == userId && uh.HouseholdId == list.HouseholdId && uh.IsActive);

            if (!userAccess)
            {
                throw new UnauthorizedAccessException("You don't have access to this household.");
            }
        }

        #endregion
    }
}
