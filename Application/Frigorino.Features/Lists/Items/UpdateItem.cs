using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public sealed record UpdateItemRequest(string? Text, QuantityDto? Quantity, bool? ClearQuantity, bool? Status, string? Comment);

    public static class UpdateItemEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateItem(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{itemId:int}", Handle)
               .WithName("UpdateItem")
               .Produces<ListItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<ListItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int listId,
            int itemId,
            UpdateItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            Quantity? quantity = null;
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed)
                {
                    return parsed.ToValidationProblem();
                }
                quantity = parsed.Value;
            }

            // Route ONLY on a text change with no explicit quantity intent — the edit composer always
            // sends a quantity or ClearQuantity, making the user authoritative. When it does route to
            // NeedsExtraction, the async LLM job re-extracts and strips the name; the user's text is
            // written as-typed here and the existing quantity is left untouched.
            var textChangedWithoutQuantityIntent =
                request.Text is not null && request.Quantity is null && request.ClearQuantity != true;

            ItemTextAnalysis? analysis = null;
            if (textChangedWithoutQuantityIntent)
            {
                analysis = ItemTextRouter.Analyze(request.Text);
            }

            var result = list.UpdateItem(itemId, request.Text, quantity, request.ClearQuantity ?? false, request.Status, request.Comment);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            if (analysis is ItemTextAnalysis routed)
            {
                quantityTrigger.OnItemRouted(householdId, listId, itemId, routed);
            }

            return TypedResults.Ok(ListItemResponse.From(result.Value));
        }
    }
}
