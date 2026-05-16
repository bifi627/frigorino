using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    // Legacy in-process task that the MaintenanceHostedService runs. The hosted service itself
    // was logically replaced by Hangfire (see CLAUDE.md); this class is left wired so existing
    // installations keep behaviour, but it has no Hangfire schedule. Drop it together with the
    // rest of the MaintenanceHostedService when that cleanup pass is scheduled.
    public class RecalculateSortOrderTask : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;

        public RecalculateSortOrderTask(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var lists = await _dbContext.Lists
                .Where(l => l.IsActive)
                .Include(l => l.ListItems)
                .ToListAsync(cancellationToken);

            foreach (var list in lists)
            {
                list.CompactItems();
            }
            if (lists.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var inventories = await _dbContext.Inventories
                .Where(i => i.IsActive)
                .Include(i => i.InventoryItems)
                .ToListAsync(cancellationToken);

            foreach (var inventory in inventories)
            {
                inventory.CompactItems();
            }
            if (inventories.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
