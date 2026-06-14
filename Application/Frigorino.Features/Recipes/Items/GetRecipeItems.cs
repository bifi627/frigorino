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
    public static class GetRecipeItemsEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeItems(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipeItems")
               .Produces<RecipeItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse[]>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            var items = await db.RecipeItems
                .Where(i => i.RecipeId == recipeId && i.IsActive)
                .OrderBy(i => i.Rank)
                .Select(RecipeItemResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(items);
        }
    }
}
