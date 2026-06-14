using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public sealed record UpdateRecipeItemRequest(string? Text, QuantityDto? Quantity, bool? ClearQuantity, string? Comment);

    public static class UpdateRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{itemId:int}", Handle)
               .WithName("UpdateRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int recipeId,
            int itemId,
            UpdateRecipeItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            Quantity? quantity = null;
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed) return parsed.ToValidationProblem();
                quantity = parsed.Value;
            }

            var textChangedWithoutQuantityIntent =
                request.Text is not null && request.Quantity is null && request.ClearQuantity != true;
            ItemTextAnalysis? analysis = textChangedWithoutQuantityIntent ? ItemTextRouter.Analyze(request.Text) : null;

            var recipe = await db.Recipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.UpdateItem(itemId, request.Text, quantity, request.ClearQuantity ?? false, request.Comment);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            if (analysis is ItemTextAnalysis routed)
            {
                quantityTrigger.OnItemRouted(householdId, recipeId, itemId, routed);
            }

            return TypedResults.Ok(RecipeItemResponse.From(result.Value));
        }
    }
}
