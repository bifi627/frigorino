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
        int ItemCount,
        int? CoverAttachmentId,
        IReadOnlyList<string> Ingredients)
    {
        // CoverAttachmentId + Ingredients are populated authoritatively by ToProjection (the list
        // endpoint, their only consumer). Create/Update load neither attachments nor items, and their
        // callers don't read these fields, so From returns the empty defaults.
        // ponytail: the spec chose to reuse this DTO across both GET slices over a list-only DTO.
        public static RecipeResponse From(Recipe recipe, User creator, int itemCount)
            => new(recipe.Id, recipe.Name, recipe.Description, recipe.Servings, recipe.HouseholdId,
                   recipe.CreatedAt, recipe.UpdatedAt,
                   new RecipeCreatorResponse(creator.ExternalId, creator.Name, creator.Email), itemCount,
                   CoverAttachmentId: null,
                   Ingredients: []);

        public static readonly Expression<Func<Recipe, RecipeResponse>> ToProjection = r => new RecipeResponse(
            r.Id, r.Name, r.Description, r.Servings, r.HouseholdId, r.CreatedAt, r.UpdatedAt,
            new RecipeCreatorResponse(r.CreatedByUser.ExternalId, r.CreatedByUser.Name, r.CreatedByUser.Email),
            r.Items.Count(x => x.IsActive),
            r.Attachments
                .Where(a => a.IsActive && a.Type == AttachmentType.Image)
                .OrderBy(a => a.Rank)
                .Select(a => (int?)a.Id)
                .FirstOrDefault(),
            r.Items
                .Where(i => i.IsActive)
                .OrderBy(i => i.Section.Rank)
                .ThenBy(i => i.Rank)
                .Select(i => i.Text)
                .ToList());
    }

    public sealed record RecipeCreatorResponse(string ExternalId, string Name, string? Email);
}
