using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public static class GetRecipeRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeRevision(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{recipeId:int}/revision", Handle)
               .WithName("GetRecipeRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var recipe = await db.Recipes
                .Where(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive)
                .Select(r => new { r.UpdatedAt })
                .FirstOrDefaultAsync(ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var items = db.RecipeItems.Where(i => i.RecipeId == recipeId && i.IsActive);
            var itemMaxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var itemCount = await items.CountAsync(ct);

            var sections = db.RecipeSections.Where(s => s.RecipeId == recipeId && s.IsActive);
            var sectionMaxUpdatedAt = await sections.MaxAsync(s => (DateTime?)s.UpdatedAt, ct);
            var sectionCount = await sections.CountAsync(ct);

            DateTime? maxUpdatedAt = itemMaxUpdatedAt;
            if (sectionMaxUpdatedAt is not null && (maxUpdatedAt is null || sectionMaxUpdatedAt > maxUpdatedAt))
            {
                maxUpdatedAt = sectionMaxUpdatedAt;
            }
            var count = itemCount + sectionCount;

            return TypedResults.Ok(RevisionResponse.Compute(recipe.UpdatedAt, maxUpdatedAt, count));
        }
    }
}
