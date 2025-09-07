using Frigorino.Infrastructure;
using Frigorino.Infrastructure.Jobs;
using Hangfire;
using Hangfire.PostgreSql;

namespace Frigorino.Web.Services
{
    public static class HangfireDependencyInjection
    {
        public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Get connection string
            var connectionString = PostgresHelper.ConvertPostgresUrlToConnectionString(configuration.GetConnectionString("Database") ?? "");

            // Add Hangfire services
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(10),
                    JobExpirationCheckInterval = TimeSpan.FromHours(1),
                    CountersAggregateInterval = TimeSpan.FromMinutes(5),
                    PrepareSchemaIfNecessary = true,
                    TransactionSynchronisationTimeout = TimeSpan.FromMinutes(5),
                    SchemaName = "hangfire"
                }));

            // Add the processing server
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = Environment.ProcessorCount;
            });

            // Register job classes
            services.AddScoped<DatabaseCleanupJob>();
            services.AddScoped<SortOrderRecalculationJob>();
            services.AddScoped<DatabaseHealthCheckJob>();
            services.AddScoped<ClassifyListsJob>();

            return services;
        }

        public static void ConfigureHangfireJobs()
        {
            // Set up recurring jobs
            RecurringJob.AddOrUpdate<DatabaseHealthCheckJob>(
                "database-health-check",
                job => job.ExecuteAsync(),
                Cron.Daily,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            RecurringJob.AddOrUpdate<DatabaseCleanupJob>(
                "database-cleanup",
                job => job.ExecuteAsync(),
                Cron.Daily,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            RecurringJob.AddOrUpdate<SortOrderRecalculationJob>(
                "sort-order-recalculation",
                job => job.ExecuteAsync(),
                Cron.Daily,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });


            RecurringJob.AddOrUpdate<ClassifyListsJob>(
                "classify-lists",
                job => job.ExecuteAsync(),
                Cron.Never,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            // Fire-and-forget job for immediate database health check on startup
            BackgroundJob.Enqueue<DatabaseHealthCheckJob>(job => job.ExecuteAsync());
        }
    }
}