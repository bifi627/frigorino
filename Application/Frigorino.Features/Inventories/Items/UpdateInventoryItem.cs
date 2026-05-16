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

namespace Frigorino.Features.Inventories.Items
{
    // ExpiryDate is intentionally write-through (null clears the value); Text and Quantity
    // preserve on null. See Inventory.UpdateItem for the rationale.
    public sealed record UpdateInventoryItemRequest(string? Text, string? Quantity, DateTime? ExpiryDate);

    public static class UpdateInventoryItemEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateInventoryItem(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{itemId:int}", Handle)
               .WithName("UpdateInventoryItem")
               .Produces<InventoryItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<InventoryItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int inventoryId,
            int itemId,
            UpdateInventoryItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var inventory = await db.Inventories
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var result = inventory.UpdateItem(itemId, request.Text, request.Quantity, request.ExpiryDate);
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
            return TypedResults.Ok(InventoryItemResponse.From(result.Value));
        }
    }
}
