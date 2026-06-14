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
    public static class GetRecipesEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipes(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipes")
               .Produces<RecipeResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse[]>, NotFound>> Handle(
            int householdId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var response = await db.Recipes
                .Where(r => r.HouseholdId == householdId && r.IsActive)
                .OrderByDescending(r => r.CreatedAt)
                .Select(RecipeResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
