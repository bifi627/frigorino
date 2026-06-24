using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;

namespace Frigorino.Infrastructure.Services
{
    // Registered when the feature is disabled (no API key / flag off). Always returns no suggestions,
    // so the SuggestRecipeTags endpoint is always safe to call.
    public sealed class NullRecipeTagSuggester : IRecipeTagSuggester
    {
        private static readonly IReadOnlyList<RecipeTag> Empty = [];

        public Task<IReadOnlyList<RecipeTag>> SuggestAsync(
            string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct)
            => Task.FromResult(Empty);
    }
}
