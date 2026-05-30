using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class QueuedHostedServiceTests
    {
        // A scoped service whose Id is unique per DI scope — lets a test prove
        // each work item ran in its own scope.
        private sealed class ScopeProbe
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        private static (QueuedHostedService service, BackgroundTaskQueue queue) Build()
        {
            var queue = new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);

            var provider = new ServiceCollection()
                .AddScoped<ScopeProbe>()
                .BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new QueuedHostedService(
                queue, scopeFactory, NullLogger<QueuedHostedService>.Instance);
            return (service, queue);
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
    }
}
