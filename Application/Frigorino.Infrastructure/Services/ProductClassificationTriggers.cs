using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path: enqueue the classify job onto the Cycle 1 runner. The lambda resolves the job
    // from the fresh per-work-item scope the consumer creates.
    public class QueueingProductClassificationTrigger : IProductClassificationTrigger
    {
        private readonly IBackgroundTaskQueue _queue;

        public QueueingProductClassificationTrigger(IBackgroundTaskQueue queue)
        {
            _queue = queue;
        }

        public void OnProductReferenced(int householdId, string rawName)
        {
            _queue.TryEnqueue((sp, ct) =>
                sp.GetRequiredService<IClassifyProductJob>().Run(householdId, rawName, ct));
        }
    }

    // Disabled path: classification is off (no key configured). Do nothing.
    public class NullProductClassificationTrigger : IProductClassificationTrigger
    {
        public void OnProductReferenced(int householdId, string rawName)
        {
        }
    }
}
