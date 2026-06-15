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

namespace Frigorino.Features.Recipes.Attachments
{
    public sealed record UpdateRecipeAttachmentRequest(string? Caption);

    public static class UpdateRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{attachmentId:int}", Handle)
               .WithName("UpdateRecipeAttachment")
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeAttachmentResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, int attachmentId, UpdateRecipeAttachmentRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.UpdateAttachmentCaption(attachmentId, request.Caption);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(RecipeAttachmentResponse.From(result.Value));
        }
    }
}
