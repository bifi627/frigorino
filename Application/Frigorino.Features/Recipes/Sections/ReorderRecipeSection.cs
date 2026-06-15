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
    public static class ReorderRecipeSectionEndpoint
    {
        public static IEndpointRouteBuilder MapReorderRecipeSection(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{sectionId:int}/reorder", Handle)
               .WithName("ReorderRecipeSection")
               .Produces<RecipeSectionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeSectionResponse>, NotFound>> Handle(
            int householdId, int recipeId, int sectionId, ReorderItemRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var recipe = await db.Recipes
                    .Include(r => r.Sections)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeSectionResponse?)null;

                var result = recipe.ReorderSection(sectionId, request.AfterId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException($"ReorderRecipeSection cannot map error of type {result.Errors[0].GetType().Name}.");
                }
                await db.SaveChangesAsync(ct);
                return RecipeSectionResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
