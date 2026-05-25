using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Jobs
{
    // Recurring daily cleanup (registered with MisfireHandlingMode.Relaxed in Program.cs).
    // Logs via ILogger only — the Hangfire.Console bridge mirrors output to the dashboard.
    public class CleanupInactiveEntitiesJob
    {
        private const int CompletedItemRetentionDays = 30;

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CleanupInactiveEntitiesJob> _logger;

        public CleanupInactiveEntitiesJob(ApplicationDbContext dbContext, ILogger<CleanupInactiveEntitiesJob> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Inactive-entity cleanup started.");

            var threshold = DateTime.UtcNow.AddDays(-CompletedItemRetentionDays);

            var households = await _dbContext.Households
                .Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
            var inventories = await _dbContext.Inventories
                .Where(i => !i.IsActive).ExecuteDeleteAsync(cancellationToken);
            var lists = await _dbContext.Lists
                .Where(l => !l.IsActive).ExecuteDeleteAsync(cancellationToken);
            // Purge a list item when soft-deleted, or checked off (Status) and untouched past retention.
            var listItems = await _dbContext.ListItems
                .Where(li => !li.IsActive || (li.Status && li.UpdatedAt < threshold))
                .ExecuteDeleteAsync(cancellationToken);
            var inventoryItems = await _dbContext.InventoryItems
                .Where(ii => !ii.IsActive).ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Cleanup done. Removed {Households} households, {Inventories} inventories, {Lists} lists, {ListItems} list items, {InventoryItems} inventory items.",
                households, inventories, lists, listItems, inventoryItems);
        }
    }
}
