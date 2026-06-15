using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes.Sections
{
    public sealed record RecipeSectionResponse(
        int Id,
        int RecipeId,
        string? Name,
        string? Description,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static RecipeSectionResponse From(RecipeSection s)
            => new(s.Id, s.RecipeId, s.Name, s.Description, s.Rank, s.CreatedAt, s.UpdatedAt);

        public static readonly Expression<Func<RecipeSection, RecipeSectionResponse>> ToProjection = s =>
            new RecipeSectionResponse(s.Id, s.RecipeId, s.Name, s.Description, s.Rank, s.CreatedAt, s.UpdatedAt);
    }
}
