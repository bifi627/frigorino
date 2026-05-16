using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Infrastructure.EntityFramework
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = ConvertPostgresUrlToConnectionString(configuration.GetConnectionString("Database") ?? "");


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

        public static string ConvertPostgresUrlToConnectionString(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            // Already an Npgsql key=value connection string — pass through unchanged.
            if (url.Contains("Host=", StringComparison.OrdinalIgnoreCase)
                || url.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            // Otherwise treat it as a postgres:// URL and parse it.
            var pattern = @"^postgres(?:ql)?://([^:]+):([^@]+)@([^:]+):(\d+)/(.+)$";
            var match = Regex.Match(url, pattern);

            if (!match.Success)
            {
                throw new ArgumentException(
                    "ConnectionStrings:Database must be either a postgres:// URL or a Npgsql key=value connection string (containing Host= or Server=).");
            }

            var user = match.Groups[1].Value;
            var password = match.Groups[2].Value;
            var host = match.Groups[3].Value;
            var port = match.Groups[4].Value;
            var database = match.Groups[5].Value;

            return $"User Id={user};Password={password};Server={host};Port={port};Database={database};";
        }
    }
}
