using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    public class RecalculateSortOrderTask : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IListItemService _listItemService;

        public RecalculateSortOrderTask(IListItemService listItemService, ApplicationDbContext dbContext)
        {
            _listItemService = listItemService;
            _dbContext = dbContext;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var lists = await _dbContext.Lists.Where(li => li.IsActive).Select(li => li.Id).ToListAsync();

            foreach (var listId in lists)
            {
                await _listItemService.RecalculateFullSortOrder(listId, "0", true);
            }
        }
    }
}
