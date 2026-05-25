using Frigorino.Infrastructure.EntityFramework;
using Hangfire;
using Hangfire.Console;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    // QUEUE-FIRST, SLEEP-TOLERANT. Railway free-tier sleeps on HTTP-idle, so no in-process
    // scheduler fires while suspended. Recurring jobs are permitted ONLY with sleep-tolerant
    // misfire handling (MisfireHandlingMode.Relaxed) so a missed run catches up once on wake.
    // Never rely on a job firing at a precise wall-clock time. Durable fire-and-forget queued
    // work is the primary use case; the only recurring job is the daily inactive-entity cleanup.
    public static class HangfireDependencyInjection
    {
        public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = DependencyInjection.ConvertPostgresUrlToConnectionString(
                configuration.GetConnectionString("Database") ?? "");

            services.AddHangfire(config => config
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString))
                .UseConsole());

            services.AddHangfireServer();

            // ILogger -> Hangfire.Console bridge. Registered as a logger provider; mirrors job
            // ILogger output into the dashboard console during execution (see Task 2).
            services.AddSingleton<IPerformingContextAccessor, AsyncLocalPerformingContextAccessor>();
            services.AddSingleton<ILoggerProvider, HangfireConsoleLoggerProvider>();
            GlobalJobFilters.Filters.Add(new PerformingContextCapture());

            return services;
        }
    }
}
