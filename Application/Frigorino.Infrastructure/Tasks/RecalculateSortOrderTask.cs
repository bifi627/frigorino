using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    public class RecalculateSortOrderTask : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IListItemService _listItemService;
        private readonly IInventoryService _inventoryService;

        public RecalculateSortOrderTask(IListItemService listItemService, ApplicationDbContext dbContext, IInventoryService inventoryService)
        {
            _listItemService = listItemService;
            _dbContext = dbContext;
            _inventoryService = inventoryService;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var lists = await _dbContext.Lists.Where(li => li.IsActive).Select(li => li.Id).ToListAsync();

            foreach (var listId in lists)
            {
                await _listItemService.RecalculateFullSortOrder(listId, "0", true);
            }

            var inventories = await _dbContext.Inventories.Where(ii => ii.IsActive).Select(ii => ii.Id).ToListAsync();

            foreach (var inventoryId in inventories)
            {
                await _inventoryService.RecalculateFullSortOrder(inventoryId, "0", true);
            }
        }
    }
}
