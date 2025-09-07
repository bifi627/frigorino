using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Jobs
{
    public class SortOrderRecalculationJob
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IListItemService _listItemService;
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<SortOrderRecalculationJob> _logger;

        public SortOrderRecalculationJob(
            ApplicationDbContext dbContext,
            IListItemService listItemService,
            IInventoryService inventoryService,
            ILogger<SortOrderRecalculationJob> logger)
        {
            _dbContext = dbContext;
            _listItemService = listItemService;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("🔄 Starting sort order recalculation job...");

            try
            {
                var listsProcessed = 0;
                var inventoriesProcessed = 0;

                // Recalculate sort order for all active lists
                var lists = await _dbContext.Lists
                    .Where(li => li.IsActive)
                    .Select(li => li.Id)
                    .ToListAsync();

                foreach (var listId in lists)
                {
                    await _listItemService.RecalculateFullSortOrder(listId, "0", true);
                    listsProcessed++;
                }

                // Recalculate sort order for all active inventories
                var inventories = await _dbContext.Inventories
                    .Where(ii => ii.IsActive)
                    .Select(ii => ii.Id)
                    .ToListAsync();

                foreach (var inventoryId in inventories)
                {
                    await _inventoryService.RecalculateFullSortOrder(inventoryId, "0", true);
                    inventoriesProcessed++;
                }

                _logger.LogInformation("✅ Sort order recalculation completed. Processed {ListsCount} lists and {InventoriesCount} inventories", 
                    listsProcessed, inventoriesProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error occurred during sort order recalculation");
                throw;
            }
        }
    }
}