using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    // Bridges Microsoft ILogger output into the Hangfire dashboard's per-job console.
    // As an IServerFilter it captures the running job's PerformingContext into an AsyncLocal
    // (so jobs never take a PerformContext parameter — job code stays Hangfire-free); as an
    // ILoggerProvider it hands out loggers that write to that captured context. The AsyncLocal
    // is static so the GlobalJobFilters instance and the DI-resolved provider coordinate without
    // sharing a reference. ProviderAlias lets appsettings scope this provider's level, e.g.
    // "Logging": { "Hangfire": { "LogLevel": { "Default": "Information" } } }.
    [ProviderAlias("Hangfire")]
    internal sealed class HangfireConsoleLoggerProvider : ILoggerProvider, IServerFilter
    {
        private static readonly AsyncLocal<PerformingContext?> CurrentContext = new();

        internal static PerformingContext? Current => CurrentContext.Value;

        public ILogger CreateLogger(string categoryName)
        {
            return new HangfireConsoleLogger();
        }

        public void OnPerforming(PerformingContext filterContext)
        {
            CurrentContext.Value = filterContext;
        }

        public void OnPerformed(PerformedContext filterContext)
        {
            CurrentContext.Value = null;
        }

        public void Dispose() { }
    }
}
