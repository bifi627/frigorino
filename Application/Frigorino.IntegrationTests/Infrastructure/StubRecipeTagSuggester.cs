using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free tag suggester for integration tests.
//   name contains "cake"/"kuchen" -> [Dessert, Baking]
//   everything else               -> [Main]
public sealed class StubRecipeTagSuggester : IRecipeTagSuggester
{
    public Task<IReadOnlyList<RecipeTag>> SuggestAsync(
        string name, string? description, IReadOnlyList<string> ingredients, CancellationToken ct)
    {
        var lower = name.ToLowerInvariant();
        IReadOnlyList<RecipeTag> tags =
            lower.Contains("cake") || lower.Contains("kuchen")
                ? new[] { RecipeTag.Dessert, RecipeTag.Baking }
                : new[] { RecipeTag.Main };
        return Task.FromResult(tags);
    }
}
