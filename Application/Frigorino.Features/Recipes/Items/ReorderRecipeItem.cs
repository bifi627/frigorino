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
    public static class ReorderRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapReorderRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{itemId:int}/reorder", Handle)
               .WithName("ReorderRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound>> Handle(
            int householdId, int recipeId, int itemId, ReorderItemRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Reorder mints a Rank from current neighbours; a concurrent same-slot reorder can
            // collide on the partial unique index. RankRetry reloads fresh state and re-mints.
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var recipe = await db.Recipes
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeItemResponse?)null;

                var result = recipe.ReorderItem(itemId, request.AfterId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException($"ReorderRecipeItem cannot map error of type {result.Errors[0].GetType().Name}.");
                }
                await db.SaveChangesAsync(ct);
                return RecipeItemResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
