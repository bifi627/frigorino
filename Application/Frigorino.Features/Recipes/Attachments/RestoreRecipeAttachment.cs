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

namespace Frigorino.Features.Recipes.Attachments
{
    public static class RestoreRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{attachmentId:int}/restore", Handle)
               .WithName("RestoreRecipeAttachment")
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeAttachmentResponse>, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Attachments)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeAttachmentResponse?)null;

                var result = recipe.RestoreAttachment(attachmentId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException(
                        $"RestoreRecipeAttachment cannot map error of type {result.Errors[0].GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    recipe.ReplaceRestoredAttachmentRank(attachmentId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return RecipeAttachmentResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
