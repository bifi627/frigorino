using FluentResults;
using Frigorino.Infrastructure.Services;

namespace Frigorino.IntegrationTests.Infrastructure;

// Network-free recipe import. URL containing "norecipe" → no_recipe_found; otherwise a fixed recipe.
public sealed class StubRecipeImportService : RecipeImportService
{
    public override Task<Result<ImportedRecipe>> ImportAsync(string url, CancellationToken ct)
    {
        if (!RecipeImportUrl.TryParseHttpUrl(url, out _))
        {
            return Task.FromResult(Result.Fail<ImportedRecipe>(
                new Error("Enter a valid http(s) URL.").WithMetadata("code", "invalid_url")));
        }

        if (url.Contains("norecipe", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Fail<ImportedRecipe>(
                new Error("Could not find a recipe on this page.").WithMetadata("code", "no_recipe_found")));
        }

        var imported = new ImportedRecipe(
            Name: "Imported Pancakes",
            Description: "From a URL",
            Servings: 4,
            Ingredients: new[] { "200g flour", "20 apples" },
            SourceName: "Example Blog");
        return Task.FromResult(Result.Ok(imported));
    }
}
