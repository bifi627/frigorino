using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class MaintenanceDependencyInjection
    {
        public static IServiceCollection AddMaintenanceServices(this IServiceCollection services)
        {
            services.AddScoped<IMaintenanceTask, DemoMaintenanceTask>();
            services.AddScoped<IMaintenanceTask, DeleteInactiveItems>();

            services.AddHostedService<MaintenanceHostedService>();

            return services;
        }
    }
}
