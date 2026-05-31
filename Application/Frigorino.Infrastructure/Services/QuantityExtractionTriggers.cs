using System.Text.RegularExpressions;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path. Digit-gate: only digit-bearing text pays the LLM. Digit present -> enqueue
    // extraction (which chains to classification on the clean name). No digit -> classify the raw
    // text directly (Cycle 2 behavior; nothing to extract).
    public class QueueingQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private static readonly Regex Digit = new(@"\d", RegexOptions.Compiled);

        private readonly IBackgroundTaskQueue _queue;
        private readonly IProductClassificationTrigger _classificationTrigger;

        public QueueingQuantityExtractionTrigger(
            IBackgroundTaskQueue queue, IProductClassificationTrigger classificationTrigger)
        {
            _queue = queue;
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemEntered(int householdId, int listId, int itemId, string rawText)
        {
            if (Digit.IsMatch(rawText))
            {
                _queue.TryEnqueue((sp, ct) =>
                    sp.GetRequiredService<IExtractQuantityJob>().Run(householdId, listId, itemId, rawText, ct));
            }
            else
            {
                _classificationTrigger.OnProductReferenced(householdId, rawText);
            }
        }
    }

    // Disabled path: extraction is off. Classification still runs on the raw text.
    public class NullQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private readonly IProductClassificationTrigger _classificationTrigger;

        public NullQuantityExtractionTrigger(IProductClassificationTrigger classificationTrigger)
        {
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemEntered(int householdId, int listId, int itemId, string rawText)
        {
            _classificationTrigger.OnProductReferenced(householdId, rawText);
        }
    }
}
