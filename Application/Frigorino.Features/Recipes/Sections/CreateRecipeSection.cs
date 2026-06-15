using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Sections
{
    public sealed record CreateRecipeSectionRequest(string? Name, string? Description);

    public static class CreateRecipeSectionEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeSection(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeSection")
               .Produces<RecipeSectionResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeSectionResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, CreateRecipeSectionRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Sections)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                var result = recipe.AddSection(request.Name, request.Description);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);
                return new CreateOutcome(RecipeSectionResponse.From(result.Value), NotFound: false, Problem: null);
            });

            if (outcome.NotFound) return TypedResults.NotFound();
            if (outcome.Problem is not null) return outcome.Problem;

            var response = outcome.Response!;
            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipeId}/sections/{response.Id}", response);
        }

        private sealed record CreateOutcome(RecipeSectionResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
