using Frigorino.Domain.Entities;

namespace Frigorino.Domain.Interfaces
{
    // The ONLY recipe-tag AI abstraction. The OpenAI SDK never crosses this boundary into
    // Domain/Features. Called synchronously and on-demand by the SuggestRecipeTags slice (the
    // deliberate, narrow exception to "AI runs fire-and-forget"). Returns the suggested tags
    // directly: an empty list means "no confident suggestions" (also the disabled/no-op result),
    // and adapter errors are swallowed to empty so the user's tap never fails.
    public interface IRecipeTagSuggester
    {
        Task<IReadOnlyList<RecipeTag>> SuggestAsync(
            string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct);
    }
}
