using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Sections
{
    public static class GetRecipeSectionsEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeSections(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipeSections")
               .Produces<RecipeSectionResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeSectionResponse[]>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            var sections = await db.RecipeSections
                .Where(s => s.RecipeId == recipeId && s.IsActive)
                .OrderBy(s => s.Rank)
                .Select(RecipeSectionResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(sections);
        }
    }
}
