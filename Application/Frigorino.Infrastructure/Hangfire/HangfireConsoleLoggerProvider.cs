using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    // ProviderAlias lets appsettings scope this provider's level, e.g.
    // "Logging": { "Hangfire": { "LogLevel": { "Default": "Information" } } }
    [ProviderAlias("Hangfire")]
    internal sealed class HangfireConsoleLoggerProvider : ILoggerProvider
    {
        private readonly IPerformingContextAccessor _accessor;

        public HangfireConsoleLoggerProvider(IPerformingContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new HangfireConsoleLogger(_accessor);
        }

        public void Dispose() { }
    }
}
