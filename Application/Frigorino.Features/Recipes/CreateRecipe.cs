using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Recipes
{
    public sealed record CreateRecipeRequest(string Name, string? Description);

    public static class CreateRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            CreateRecipeRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var creator = membership.User;
            var creation = Recipe.Create(request.Name, request.Description, householdId, currentUser.UserId);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var recipe = creation.Value;
            recipe.CreatedByUser = creator;
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct);

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipe.Id}",
                RecipeResponse.From(recipe, creator, itemCount: 0));
        }
    }
}
