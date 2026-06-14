using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public sealed record UpdateRecipeRequest(string Name, string? Description);

    public static class UpdateRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{recipeId:int}", Handle)
               .WithName("UpdateRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status403Forbidden)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId, int recipeId, UpdateRecipeRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var recipe = await db.Recipes
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var result = recipe.Update(currentUser.UserId, membership.Role, request.Name, request.Description);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            var itemCount = await db.RecipeItems.CountAsync(i => i.RecipeId == recipeId && i.IsActive, ct);
            return TypedResults.Ok(RecipeResponse.From(recipe, recipe.CreatedByUser, itemCount));
        }
    }
}
