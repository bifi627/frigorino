using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path. NeedsExtraction enqueues the extract job, which extracts from the raw text and
    // chains classification on its own clean name. SkipAi (URL/junk) does nothing — no extraction,
    // no classification.
    public class QueueingQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private readonly IBackgroundTaskQueue _queue;

        public QueueingQuantityExtractionTrigger(IBackgroundTaskQueue queue)
        {
            _queue = queue;
        }

        public void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis)
        {
            switch (analysis.Route)
            {
                case ItemTextRoute.NeedsExtraction:
                    _queue.TryEnqueue((sp, ct) =>
                        sp.GetRequiredService<IExtractQuantityJob>()
                          .Run(householdId, listId, itemId, analysis.CleanName, ct));
                    break;
                case ItemTextRoute.SkipAi:
                default:
                    break;
            }
        }
    }

    // Disabled path: extraction is off. Every non-skip route classifies the clean name (for
    // NeedsExtraction the clean name equals the raw text — nothing was stripped). SkipAi does nothing.
    public class NullQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private readonly IProductClassificationTrigger _classificationTrigger;

        public NullQuantityExtractionTrigger(IProductClassificationTrigger classificationTrigger)
        {
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis)
        {
            if (analysis.Route != ItemTextRoute.SkipAi)
            {
                _classificationTrigger.OnProductReferenced(householdId, analysis.CleanName);
            }
        }
    }
}
