using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public static class GetRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipe(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{recipeId:int}", Handle)
               .WithName("GetRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var response = await db.Recipes
                .Where(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive)
                .Select(RecipeResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
