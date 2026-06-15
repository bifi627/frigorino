using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes.Links
{
    public sealed record RecipeLinkResponse(
        int Id,
        int RecipeId,
        string Url,
        string? Label,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static RecipeLinkResponse From(RecipeLink l)
            => new(l.Id, l.RecipeId, l.Url, l.Label, l.Rank, l.CreatedAt, l.UpdatedAt);

        public static readonly Expression<Func<RecipeLink, RecipeLinkResponse>> ToProjection = l =>
            new RecipeLinkResponse(l.Id, l.RecipeId, l.Url, l.Label, l.Rank, l.CreatedAt, l.UpdatedAt);
    }
}
