using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class QueuedHostedServiceTests : IDisposable
    {
        // Providers built during a test, disposed at teardown to avoid leaking IDisposable scopes.
        private readonly List<ServiceProvider> _providers = new();

        // A scoped service whose Id is unique per DI scope — lets a test prove
        // each work item ran in its own scope.
        private sealed class ScopeProbe
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        // Captures emitted log levels so a test can assert what was (and was not) logged.
        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<LogLevel> Levels { get; } = new();

            IDisposable? ILogger.BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Levels.Add(logLevel);
            }
        }

        private IServiceScopeFactory NewScopeFactory()
        {
            var provider = new ServiceCollection()
                .AddScoped<ScopeProbe>()
                .BuildServiceProvider();
            _providers.Add(provider);
            return provider.GetRequiredService<IServiceScopeFactory>();
        }

        private (QueuedHostedService service, BackgroundTaskQueue queue) Build()
        {
            var queue = new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);
            var service = new QueuedHostedService(
                queue, NewScopeFactory(), NullLogger<QueuedHostedService>.Instance);
            return (service, queue);
        }

        public void Dispose()
        {
            foreach (var provider in _providers)
            {
                provider.Dispose();
            }
        }

        [Fact]
        public async Task ExecuteAsync_RunsEnqueuedWork()
        {
            var (service, queue) = Build();
            var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue((_, _) =>
            {
                ran.SetResult();
                return Task.CompletedTask;
            });

            await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None);

            Assert.True(ran.Task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task ExecuteAsync_CreatesFreshScopePerItem()
        {
            var (service, queue) = Build();
            var first = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            var second = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue((sp, _) =>
            {
                first.SetResult(sp.GetRequiredService<ScopeProbe>().Id);
                return Task.CompletedTask;
            });
            queue.TryEnqueue((sp, _) =>
            {
                second.SetResult(sp.GetRequiredService<ScopeProbe>().Id);
                return Task.CompletedTask;
            });

            var firstId = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var secondId = await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None);

            // Different scopes => different scoped ScopeProbe instances.
            Assert.NotEqual(firstId, secondId);
        }

        [Fact]
        public async Task ExecuteAsync_ThrowingItem_DoesNotStopConsumer()
        {
            var (service, queue) = Build();
            var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue((_, _) => throw new InvalidOperationException("boom"));
            queue.TryEnqueue((_, _) =>
            {
                second.SetResult();
                return Task.CompletedTask;
            });

            // The throwing item must be swallowed; the next item must still run.
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None);

            Assert.True(second.Task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task StopAsync_CompletesCleanly()
        {
            var (service, _) = Build();

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None); // must not throw
        }

        [Fact]
        public async Task StopAsync_WhileItemRunning_DoesNotLogError()
        {
            var queue = new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);
            var logger = new CapturingLogger<QueuedHostedService>();
            var service = new QueuedHostedService(queue, NewScopeFactory(), logger);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue(async (_, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct); // throws OCE when the host stops
            });

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None); // cancels stoppingToken -> item's Task.Delay throws OCE

            // The in-flight item was cancelled by shutdown, not a fault: it must NOT be logged as an error.
            Assert.DoesNotContain(LogLevel.Error, logger.Levels);
        }
    }
}
