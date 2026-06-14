using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public sealed record CreateRecipeItemRequest(string Text, string? Comment);

    public static class CreateRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int recipeId,
            CreateRecipeItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var analysis = ItemTextRouter.Analyze(request.Text);

            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                var result = recipe.AddItem(analysis.CleanName, quantity: null, request.Comment);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);

                var resp = RecipeItemResponse.From(result.Value) with
                {
                    ExtractionPending = analysis.Route == ItemTextRoute.NeedsExtraction,
                };
                return new CreateOutcome(resp, NotFound: false, Problem: null);
            });

            if (outcome.NotFound) return TypedResults.NotFound();
            if (outcome.Problem is not null) return outcome.Problem;

            var response = outcome.Response!;
            quantityTrigger.OnItemRouted(householdId, recipeId, response.Id, analysis);

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipeId}/items/{response.Id}",
                response);
        }

        private sealed record CreateOutcome(RecipeItemResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
