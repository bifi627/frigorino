using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Frigorino.IntegrationTests.Infrastructure;

/// <summary>
/// Thread-safe, in-memory buffer of formatted server-side log records for one host instance.
/// Kestrel handles requests off the test's execution context, so the host's AddConsole() output
/// isn't attributed to the failing scenario by the xUnit runner. Buffering records here and
/// dumping them from an AfterScenario hook (which DOES run on the test context) closes that gap —
/// the framework already logs unhandled exceptions at Error level, this just makes them visible.
/// </summary>
public sealed class InMemoryLogSink
{
    private readonly ConcurrentQueue<string> _entries = new();

    public void Add(string entry) => _entries.Enqueue(entry);

    public IReadOnlyList<string> Snapshot() => _entries.ToArray();
}

public sealed class InMemoryLoggerProvider(InMemoryLogSink sink, LogLevel minLevel) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(sink, categoryName, minLevel);

    public void Dispose() { }

    private sealed class InMemoryLogger(InMemoryLogSink sink, string category, LogLevel minLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"[{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            sink.Add(line);
        }
    }
}
