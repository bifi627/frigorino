using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Links
{
    public static class RestoreRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{linkId:int}/restore", Handle)
               .WithName("RestoreRecipeLink")
               .Produces<RecipeLinkResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeLinkResponse>, NotFound>> Handle(
            int householdId, int recipeId, int linkId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            // Restore reactivates the link with its ORIGINAL rank to preserve position. If a live
            // link took that rank while it was deleted, the partial unique index rejects it; on that
            // retry we re-mint to the end.
            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Links)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeLinkResponse?)null;

                var result = recipe.RestoreLink(linkId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException(
                        $"RestoreRecipeLink cannot map error of type {result.Errors[0].GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    recipe.ReplaceRestoredLinkRank(linkId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return RecipeLinkResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
