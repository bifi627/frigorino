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

            var result = list.UpdateItem(itemId, request.Text, quantity, request.ClearQuantity ?? false, request.Status);
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

            // Re-extract on a text change ONLY when the caller expressed no explicit quantity
            // intent. The edit composer always sends a quantity (or ClearQuantity = true), making
            // the user authoritative — re-extraction would race ExtractQuantityJob and silently
            // clobber the value they just set.
            if (request.Text is not null && request.Quantity is null && request.ClearQuantity != true)
            {
                quantityTrigger.OnItemEntered(householdId, listId, itemId, request.Text);
            }

            return TypedResults.Ok(ListItemResponse.From(result.Value));
        }
    }
}
