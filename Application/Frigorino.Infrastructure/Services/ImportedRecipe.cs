namespace Frigorino.Infrastructure.Services
{
    // Plain data carrier produced by RecipeImportService (NOT an entity). The ImportRecipe slice maps
    // this onto Recipe.Create / AddSection / AddItem / AddLink.
    public sealed record ImportedRecipe(
        string Name,
        string? Description,
        int? Servings,
        IReadOnlyList<string> Ingredients,
        string? SourceName,
        string? ImageUrl = null);
}
