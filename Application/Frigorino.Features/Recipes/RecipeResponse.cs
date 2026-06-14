using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes
{
    public sealed record RecipeResponse(
        int Id,
        string Name,
        string? Description,
        int? Servings,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        RecipeCreatorResponse CreatedByUser,
        int ItemCount)
    {
        public static RecipeResponse From(Recipe recipe, User creator, int itemCount)
            => new(recipe.Id, recipe.Name, recipe.Description, recipe.Servings, recipe.HouseholdId,
                   recipe.CreatedAt, recipe.UpdatedAt,
                   new RecipeCreatorResponse(creator.ExternalId, creator.Name, creator.Email), itemCount);

        public static readonly Expression<Func<Recipe, RecipeResponse>> ToProjection = r => new RecipeResponse(
            r.Id, r.Name, r.Description, r.Servings, r.HouseholdId, r.CreatedAt, r.UpdatedAt,
            new RecipeCreatorResponse(r.CreatedByUser.ExternalId, r.CreatedByUser.Name, r.CreatedByUser.Email),
            r.Items.Count(x => x.IsActive));
    }

    public sealed record RecipeCreatorResponse(string ExternalId, string Name, string? Email);
}
