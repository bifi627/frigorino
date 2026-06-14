using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // Recipe analog of IQuantityExtractionTrigger. CRUCIAL DIFFERENCE: it never chains
    // classification — recipe items must NOT create Product rows (MVP decision). NeedsExtraction
    // enqueues the recipe extract job; SkipAi does nothing on either impl.
    public interface IRecipeQuantityExtractionTrigger
    {
        void OnItemRouted(int householdId, int recipeId, int itemId, ItemTextAnalysis analysis);
    }
}
