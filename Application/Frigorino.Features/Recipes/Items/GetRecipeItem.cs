using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public static class GetRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}", Handle)
               .WithName("GetRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound>> Handle(
            int householdId, int recipeId, int itemId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var item = await db.RecipeItems
                .Where(i => i.Id == itemId && i.RecipeId == recipeId && i.IsActive
                            && i.Recipe.HouseholdId == householdId && i.Recipe.IsActive)
                .Select(RecipeItemResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            return item is null ? TypedResults.NotFound() : TypedResults.Ok(item);
        }
    }
}
