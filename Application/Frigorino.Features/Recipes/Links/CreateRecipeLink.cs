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

namespace Frigorino.Features.Recipes.Links
{
    public sealed record CreateRecipeLinkRequest(string Url, string? Label);

    public static class CreateRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeLink")
               .Produces<RecipeLinkResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeLinkResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, CreateRecipeLinkRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Links)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                var result = recipe.AddLink(request.Url, request.Label);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);
                return new CreateOutcome(RecipeLinkResponse.From(result.Value), NotFound: false, Problem: null);
            });

            if (outcome.NotFound) return TypedResults.NotFound();
            if (outcome.Problem is not null) return outcome.Problem;

            var response = outcome.Response!;
            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipeId}/links/{response.Id}", response);
        }

        private sealed record CreateOutcome(RecipeLinkResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
