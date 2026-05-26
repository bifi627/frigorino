using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    public class MaintenanceHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MaintenanceHostedService> _logger;

        public MaintenanceHostedService(IServiceProvider serviceProvider, ILogger<MaintenanceHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Maintenance hosted service is starting...");

            // Wait a bit for the application to fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var maintenanceServices = scope.ServiceProvider.GetServices<IMaintenanceTask>();

                foreach (var task in maintenanceServices)
                {
                    try
                    {
                        await task.Run(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $" Maintenance task {task.GetType().Name} failed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Failed to run startup maintenance tasks");
                // Don't throw - we don't want to crash the application
            }
        }
    }
}
