using Hangfire.Console;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    internal sealed class HangfireConsoleLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var context = HangfireConsoleLoggerProvider.Current;
            if (context is null)
            {
                return;
            }

            // Pass null exception to the formatter, then append it ourselves below — avoids the
            // default formatter double-printing the exception.
            var message = $"{logLevel}: {formatter(state, null)}";
            if (exception is not null)
            {
                message += Environment.NewLine + exception;
            }

            context.WriteLine(GetColor(logLevel), message);
        }

        private static ConsoleTextColor GetColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Critical or LogLevel.Error => ConsoleTextColor.Red,
                LogLevel.Warning => ConsoleTextColor.Yellow,
                LogLevel.Information => ConsoleTextColor.White,
                _ => ConsoleTextColor.Gray,
            };
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
