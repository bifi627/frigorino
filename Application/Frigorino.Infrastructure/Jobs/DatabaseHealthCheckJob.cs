using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Jobs
{
    public class DatabaseHealthCheckJob
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DatabaseHealthCheckJob> _logger;

        public DatabaseHealthCheckJob(ApplicationDbContext dbContext, ILogger<DatabaseHealthCheckJob> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("🔍 Starting database health check job...");

            try
            {
                // Simple connectivity check
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                
                // Get basic statistics
                var activeHouseholds = await _dbContext.Households.CountAsync(h => h.IsActive);
                var activeLists = await _dbContext.Lists.CountAsync(l => l.IsActive);
                var activeListItems = await _dbContext.ListItems.CountAsync(li => li.IsActive);
                var activeInventories = await _dbContext.Inventories.CountAsync(i => i.IsActive);
                var activeInventoryItems = await _dbContext.InventoryItems.CountAsync(ii => ii.IsActive);

                _logger.LogInformation("💚 Database health check completed. Stats: " +
                    "{ActiveHouseholds} households, {ActiveLists} lists, {ActiveListItems} list items, " +
                    "{ActiveInventories} inventories, {ActiveInventoryItems} inventory items",
                    activeHouseholds, activeLists, activeListItems, activeInventories, activeInventoryItems);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Database health check failed");
                throw;
            }
        }
    }
}