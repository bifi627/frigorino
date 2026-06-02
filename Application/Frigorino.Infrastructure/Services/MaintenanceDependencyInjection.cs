using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class MaintenanceDependencyInjection
    {
        public static IServiceCollection AddMaintenanceServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IMaintenanceTask, DeleteInactiveItems>();

            // Registered only when live classification is on (same condition as
            // ItemClassificationDependencyInjection); otherwise IItemClassifier and the queueing
            // trigger are not in the container and ValidateOnBuild would fail.
            var classificationEnabled = configuration.GetValue<bool>("Ai:Classifier:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            if (classificationEnabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddScoped<IMaintenanceTask, BackfillProductClassification>();
            }

            services.AddHostedService<MaintenanceHostedService>();

            return services;
        }
    }
}
