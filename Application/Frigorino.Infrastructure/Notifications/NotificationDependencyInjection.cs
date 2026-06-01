using Frigorino.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class NotificationDependencyInjection
    {
        public static IServiceCollection AddExpiryNotifications(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MaintenanceSettings>(
                configuration.GetSection(MaintenanceSettings.SECTION_NAME));

            services.AddScoped<ExpiryNotificationScan>();
            return services;
        }
    }
}
