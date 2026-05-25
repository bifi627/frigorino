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
            // Hangfire may use its own database/schema; falls back to the app DB when unset.
            var rawConnectionString = configuration.GetConnectionString("Hangfire");
            if (string.IsNullOrWhiteSpace(rawConnectionString))
            {
                rawConnectionString = configuration.GetConnectionString("Database");
            }

            var connectionString = DependencyInjection.ConvertPostgresUrlToConnectionString(
                rawConnectionString ?? "");

            services.AddHangfire(config => config
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString))
                // Keep a week of processed-job history (default is 24h) for post-mortem visibility.
                .WithJobExpirationTimeout(TimeSpan.FromDays(7))
                .UseConsole());

            services.AddHangfireServer();

            // ILogger -> Hangfire.Console bridge. One type plays two roles: as an IServerFilter it
            // captures the running job's context; as an ILoggerProvider it emits captured logs to
            // the dashboard console. They coordinate via a static AsyncLocal, so the GlobalJobFilters
            // instance and the DI-resolved provider don't need to be the same object.
            services.AddSingleton<ILoggerProvider, HangfireConsoleLoggerProvider>();
            GlobalJobFilters.Filters.Add(new HangfireConsoleLoggerProvider());

            return services;
        }
    }
}
