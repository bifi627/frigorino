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

        public async Task<ListItemDto> CreateItemAsync(int listId, CreateListItemRequest request, string userId, bool @checked = false)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("Item text is required.");
            }

            // Validate user access to the list
            await ValidateListAccessAsync(listId, userId);

            // Calculate sort order for new item (always goes to top of unchecked section)
            // In realistic usage, items are added one at a time, so we use the standard approach
            var existingUncheckedItems = await _dbContext.ListItems
                .Where(li => li.ListId == listId && li.IsActive)
                .OrderBy(li => li.SortOrder)
                .ToListAsync();

            var sortOrder = UpdateSortOrder(existingUncheckedItems, @checked, null, null);

            var item = request.ToEntity(listId, sortOrder);
            item.Status = @checked; // Set initial status based on parameter
            _dbContext.ListItems.Add(item);
            await _dbContext.SaveChangesAsync();

            return item.ToDto();
        }

        public async Task<ListItemDto?> UpdateItemAsync(int itemId, UpdateListItemRequest request, string userId)
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

            // If status is changing, update sort order accordingly
            var statusChanged = item.Status != request.Status;

            if (statusChanged && request.Status is not null)
            {
                var allEntries = item.List.ListItems;

                var sortOrder = UpdateSortOrder(allEntries, request.Status.Value, null, null);
                item.SortOrder = sortOrder;
            }

            item.UpdateFromRequest(request);

            await _dbContext.SaveChangesAsync();
            return item.ToDto();
        }

        public static int UpdateSortOrder(IEnumerable<Domain.Entities.ListItem> allEntries, bool status, int? after, int? before)
        {
            var existingItems = allEntries.Where(x => x.Status == status).OrderBy(x => x.SortOrder).ToList();
            var first = existingItems.FirstOrDefault()?.SortOrder ?? null;
            var last = existingItems.LastOrDefault()?.SortOrder ?? null;

            var result = SortOrderCalculator.CalculateSortOrder(status, after, before, first, last, out bool needRecalculation);

            return result ?? -1;
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
                .Include(li => li.List).ThenInclude(item => item.ListItems)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return null;
            }

            // Validate user access
            await ValidateListAccessAsync(item.ListId, userId);

            var items = item.List.ListItems.Where(li => li.Status == item.Status).OrderBy(l => l.SortOrder).ToArray();
            var afterItem = items.FirstOrDefault(item => item.Id == request.AfterId);
            ListItem? beforeItem = null;
            if (afterItem is not null)
            {
                beforeItem = items.FirstOrDefault(item => item.SortOrder > afterItem.SortOrder);
            }

            var sortOrder = UpdateSortOrder(item.List.ListItems, item.Status, afterItem?.SortOrder ?? 0, beforeItem?.SortOrder ?? 0);
            item.SortOrder = sortOrder;

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
                .Include(li => li.List).ThenInclude(l => l.ListItems)
                .FirstOrDefaultAsync(li => li.Id == itemId && li.IsActive);

            if (item == null)
            {
                return null;
            }

            // Validate user access
            await ValidateListAccessAsync(item.ListId, userId);

            var sortOrder = UpdateSortOrder(item.List.ListItems, !item.Status, null, null);
            item.SortOrder = sortOrder;

            item.Status = !item.Status; // Toggle status

            await _dbContext.SaveChangesAsync();
            return item.ToDto();
        }

        public async Task<bool> RecalculateFullSortOrder(int listId, string userId)
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
