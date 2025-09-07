using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Jobs
{
    public class DatabaseCleanupJob
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DatabaseCleanupJob> _logger;

        public DatabaseCleanupJob(ApplicationDbContext dbContext, ILogger<DatabaseCleanupJob> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("🧹 Starting database cleanup job...");

            try
            {
                var deletedCount = 0;

                // Delete inactive households
                var householdsDeleted = await _dbContext.Households
                    .Where(h => !h.IsActive)
                    .ExecuteDeleteAsync();
                deletedCount += householdsDeleted;

                // Delete inactive inventories
                var inventoriesDeleted = await _dbContext.Inventories
                    .Where(h => !h.IsActive)
                    .ExecuteDeleteAsync();
                deletedCount += inventoriesDeleted;

                // Delete inactive lists
                var listsDeleted = await _dbContext.Lists
                    .Where(li => !li.IsActive)
                    .ExecuteDeleteAsync();
                deletedCount += listsDeleted;

                // Delete inactive list items
                var listItemsDeleted = await _dbContext.ListItems
                    .Where(li => !li.IsActive)
                    .ExecuteDeleteAsync();
                deletedCount += listItemsDeleted;

                // Delete inactive inventory items
                var inventoryItemsDeleted = await _dbContext.InventoryItems
                    .Where(h => !h.IsActive)
                    .ExecuteDeleteAsync();
                deletedCount += inventoryItemsDeleted;

                _logger.LogInformation("✅ Database cleanup completed. Deleted {DeletedCount} records", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error occurred during database cleanup");
                throw;
            }
        }
    }
}