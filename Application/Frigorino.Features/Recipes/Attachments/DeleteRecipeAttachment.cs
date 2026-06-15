using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class DeleteRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{attachmentId:int}", Handle)
               .WithName("DeleteRecipeAttachment")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.RemoveAttachment(attachmentId);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                throw new InvalidOperationException(
                    $"DeleteRecipeAttachment cannot map error of type {result.Errors[0].GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
