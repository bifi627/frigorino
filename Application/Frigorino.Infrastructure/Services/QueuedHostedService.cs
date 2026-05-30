using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Single consumer that drains BackgroundTaskQueue, running each work item in its own DI scope.
    // Event-driven: ReadAllAsync parks at zero CPU when the queue is empty.
    public class QueuedHostedService : BackgroundService
    {
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
            try
            {
                await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        await work(scope.ServiceProvider, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // The item was cancelled by host shutdown — not a fault. Rethrow so the outer
                        // handler stops the loop cleanly instead of logging it as a work-item error.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // One bad item must never take down the consumer or starve the queue.
                        _logger.LogError(ex, "Background work item failed.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — the stopping token fired (from ReadAllAsync, or rethrown above);
                // stop draining and abandon whatever is still queued (lossy by design).
            }
        }
    }
}
