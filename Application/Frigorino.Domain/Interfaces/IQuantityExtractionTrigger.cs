using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // Single front door the list-item slices call after computing an ItemTextAnalysis. The enabled
    // implementation enqueues the extract job for NeedsExtraction (chaining to classification on the
    // clean name) and classifies directly for Resolved/ClassifyOnly; the disabled implementation
    // classifies the clean name for every non-skip route. SkipAi does nothing on either.
    public interface IQuantityExtractionTrigger
    {
        void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis);
    }
}
