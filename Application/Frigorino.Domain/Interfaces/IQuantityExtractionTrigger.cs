using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // Single front door the list-item slices call after computing an ItemTextAnalysis. The enabled
    // implementation enqueues the extract job for NeedsExtraction (which chains to classification on
    // the clean name); the disabled implementation classifies the clean name directly for every
    // non-skip route. SkipAi does nothing on either.
    public interface IQuantityExtractionTrigger
    {
        void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis);
    }
}
