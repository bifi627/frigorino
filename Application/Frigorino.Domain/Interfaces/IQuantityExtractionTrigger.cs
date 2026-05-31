namespace Frigorino.Domain.Interfaces
{
    // Called by the list-item slices after an item's text is entered or changed. The enabled
    // implementation digit-gates and enqueues the extract job (which chains to classification on
    // the clean name); the disabled implementation skips extraction and classifies the raw text.
    // This is the single front door the slices call — classification hangs off of it.
    public interface IQuantityExtractionTrigger
    {
        void OnItemEntered(int householdId, int listId, int itemId, string rawText);
    }
}
