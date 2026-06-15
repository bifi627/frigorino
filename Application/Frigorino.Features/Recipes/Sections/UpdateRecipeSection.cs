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

namespace Frigorino.Features.Recipes.Sections
{
    public sealed record UpdateRecipeSectionRequest(string? Name, string? Description);

    public static class UpdateRecipeSectionEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipeSection(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{sectionId:int}", Handle)
               .WithName("UpdateRecipeSection")
               .Produces<RecipeSectionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeSectionResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, int sectionId, UpdateRecipeSectionRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Sections)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.UpdateSection(sectionId, request.Name, request.Description);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(RecipeSectionResponse.From(result.Value));
        }
    }
}
