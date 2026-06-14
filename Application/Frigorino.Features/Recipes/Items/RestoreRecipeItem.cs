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

namespace Frigorino.Features.Recipes.Items
{
    public static class RestoreRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{itemId:int}/restore", Handle)
               .WithName("RestoreRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound>> Handle(
            int householdId,
            int recipeId,
            int itemId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Restore reactivates the item with its ORIGINAL rank to preserve its undo position.
            // If a live item took that slot while it was deleted, the partial unique index rejects
            // it; on that retry we re-mint to the end of the section so the restore still succeeds.
            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null)
                {
                    return (RecipeItemResponse?)null;
                }

                var result = recipe.RestoreItem(itemId);
                if (result.IsFailed)
                {
                    var first = result.Errors[0];
                    if (first is EntityNotFoundError)
                    {
                        return null;
                    }
                    throw new InvalidOperationException(
                        $"RestoreRecipeItem cannot map error of type {first.GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    recipe.ReplaceRestoredItemRank(itemId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return RecipeItemResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
