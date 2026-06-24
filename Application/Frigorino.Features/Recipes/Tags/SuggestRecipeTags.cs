using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Tags
{
    public sealed record SuggestRecipeTagsResponse(IReadOnlyList<RecipeTag> SuggestedTags);

    public static class SuggestRecipeTagsEndpoint
    {
        public static IEndpointRouteBuilder MapSuggestRecipeTags(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{recipeId:int}/suggest-tags", Handle)
               .WithName("SuggestRecipeTags")
               .Produces<SuggestRecipeTagsResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        // Synchronous, on-demand AI — the deliberate, narrow exception to "AI never inline" (this is
        // the user's primary action, not a side-effect of a write). Stateless: nothing is persisted.
        private static async Task<Results<Ok<SuggestRecipeTagsResponse>, NotFound>> Handle(
            int householdId, int recipeId,
            ICurrentUserService currentUser, IRecipeTagSuggester suggester,
            ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var recipe = await db.Recipes
                .Where(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive)
                .Select(r => new { r.Name, r.Description })
                .FirstOrDefaultAsync(ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var ingredients = await db.RecipeItems
                .Where(i => i.RecipeId == recipeId && i.IsActive)
                .OrderBy(i => i.Section.Rank)
                .ThenBy(i => i.Rank)
                .Select(i => i.Text)
                .ToListAsync(ct);

            var suggested = await suggester.SuggestAsync(recipe.Name, recipe.Description, ingredients, ct);
            return TypedResults.Ok(new SuggestRecipeTagsResponse(suggested));
        }
    }
}
