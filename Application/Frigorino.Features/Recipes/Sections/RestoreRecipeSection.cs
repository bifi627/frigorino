using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Sections
{
    public static class RestoreRecipeSectionEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreRecipeSection(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{sectionId:int}/restore", Handle)
               .WithName("RestoreRecipeSection")
               .Produces<RecipeSectionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeSectionResponse>, NotFound>> Handle(
            int householdId, int recipeId, int sectionId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            // Restore reactivates the section with its ORIGINAL rank to preserve its position. If a
            // live section took that rank while it was deleted, the partial unique index rejects it;
            // on that retry we re-mint to the end. Items inside the section keep their own ranks
            // (nothing joined a deleted section, so they never collide).
            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Sections)
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeSectionResponse?)null;

                var result = recipe.RestoreSection(sectionId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException(
                        $"RestoreRecipeSection cannot map error of type {result.Errors[0].GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    recipe.ReplaceRestoredSectionRank(sectionId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return RecipeSectionResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
