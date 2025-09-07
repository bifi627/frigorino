using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.EntityFramework
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = PostgresHelper.ConvertPostgresUrlToConnectionString(configuration.GetConnectionString("Database") ?? "");

            services.AddDbContext<ApplicationDbContext>(contextOptions =>
            {
                contextOptions.UseNpgsql(connectionString, postgresOptions =>
                {
                    postgresOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                });
            });

            // Register household service
            services.AddScoped<ICurrentHouseholdService, CurrentHouseholdService>();

            return services;
        }
    }
}
