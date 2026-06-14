using Frigorino.Domain.Errors;
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
    public static class DeleteRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteRecipe(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{recipeId:int}", Handle)
               .WithName("DeleteRecipe")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status403Forbidden)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var recipe = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var result = recipe.SoftDelete(currentUser.UserId, membership.Role);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                throw new InvalidOperationException(
                    $"DeleteRecipe cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
