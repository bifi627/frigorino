using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Single consumer that drains BackgroundTaskQueue, running each work item in its own DI scope.
    // Event-driven: ReadAllAsync parks at zero CPU when the queue is empty.
    //
    // Work items are I/O-bound (e.g. an OpenAI classify call ~1s each), so a serial drain made a
    // large backfill take items × ~1s of wall-clock. A single reader still owns the channel
    // (SingleReader = true is preserved), but it dispatches each item to a bounded pool of up to
    // MaxConcurrency in-flight tasks instead of awaiting inline — the network waits now overlap.
    public class QueuedHostedService : BackgroundService
    {
        // Concurrent in-flight work items. Bounded (not unbounded) so a flood of queued items can't
        // hammer downstream APIs into rate limits. Items are independent and each runs in its own
        // scope, so concurrency is safe; 8 keeps comfortable headroom under typical OpenAI limits.
        public const int MaxConcurrency = 8;

        private readonly BackgroundTaskQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QueuedHostedService> _logger;

        public QueuedHostedService(
            BackgroundTaskQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<QueuedHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // One free slot per allowed in-flight item; the reader blocks on WaitAsync once all are taken.
            using var slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
            var running = new List<Task>();

            try
            {
                await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    // Park the (single) reader until a slot frees up — this is what bounds concurrency.
                    await slots.WaitAsync(stoppingToken);
                    running.Add(RunWorkItemAsync(work, slots, stoppingToken));
                    // Drop finished tasks so the list can't grow without bound during a long backfill.
                    running.RemoveAll(t => t.IsCompleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — the stopping token fired (from ReadAllAsync or WaitAsync); stop
                // dequeuing and abandon whatever is still queued (lossy by design).
            }

            // Let in-flight items finish (or observe the stopping token themselves) before we exit.
            // RunWorkItemAsync never throws, so WhenAll can't fault.
            await Task.WhenAll(running);
        }

        // Runs one work item in its own DI scope. Swallows every fault so a bad item can never take
        // down the consumer; always releases its slot so the reader can admit the next item.
        private async Task RunWorkItemAsync(
            Func<IServiceProvider, CancellationToken, Task> work,
            SemaphoreSlim slots,
            CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await work(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The item was cancelled by host shutdown — not a fault, so don't log it as an error.
            }
            catch (Exception ex)
            {
                // One bad item must never take down the consumer or starve the queue.
                _logger.LogError(ex, "Background work item failed.");
            }
            finally
            {
                slots.Release();
            }
        }
    }
}
