using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Tasks
{
    public class DemoMaintenanceTask : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DemoMaintenanceTask> _logger;

        public DemoMaintenanceTask(ApplicationDbContext dbContext, ILogger<DemoMaintenanceTask> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("🔧 Starting maintenance tasks...");

            try
            {
                await CheckDatabaseHealthAsync(cancellationToken);

                _logger.LogInformation("✅ Maintenance tasks completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error occurred during maintenance tasks");
                throw;
            }
        }

        private async Task CheckDatabaseHealthAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔍 Checking database health...");

            try
            {
                // Simple connectivity check
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                _logger.LogInformation("💚 Database connection is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Database health check failed");
            }
        }
    }
}
