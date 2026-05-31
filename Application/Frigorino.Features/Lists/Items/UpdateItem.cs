using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public sealed record UpdateItemRequest(string? Text, QuantityDto? Quantity, bool? ClearQuantity, bool? Status);

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

            // Run the deterministic router ONLY on a text change with no explicit quantity intent —
            // same condition as the legacy re-extraction guard (the edit composer always sends a
            // quantity or ClearQuantity, making the user authoritative). On a Resolved parse we write
            // the stripped name + quantity in this same save; SkipAi/NeedsExtraction/ClassifyOnly
            // leave the user's text as-typed and the existing quantity untouched.
            ItemTextAnalysis? analysis = null;
            if (request.Text is not null && request.Quantity is null && request.ClearQuantity != true)
            {
                analysis = ItemTextRouter.Analyze(request.Text);
            }

            var textToWrite = request.Text;
            if (analysis is { Route: ItemTextRoute.Resolved } resolved)
            {
                textToWrite = resolved.CleanName;
                quantity = resolved.Quantity;
            }

            var result = list.UpdateItem(itemId, textToWrite, quantity, request.ClearQuantity ?? false, request.Status);
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
