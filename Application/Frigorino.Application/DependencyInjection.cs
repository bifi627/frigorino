using Frigorino.Application.Services;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IHouseholdService, HouseholdService>();
            services.AddScoped<IListService, ListService>();
            services.AddScoped<IListItemService, ListItemService>();
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddScoped<IInventoryItemService, InventoryItemService>();
            
            return services;
        }
    }
}
