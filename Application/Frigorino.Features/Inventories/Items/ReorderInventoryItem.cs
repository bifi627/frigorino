using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Items
{
    // AfterId == 0 means "move to the top of the section". An AfterId that doesn't resolve to
    // an active sibling silently falls back to top-of-section — preserves the wire contract the
    // frontend's optimistic UI depends on. Same shape as the ListItems ReorderItemRequest;
    // OpenAPI deduplicates same-name same-shape types so the generated TS client emits a single
    // ReorderItemRequest type shared between listItems and inventoryItems services.
    public sealed record ReorderItemRequest(int AfterId);

    public static class ReorderInventoryItemEndpoint
    {
        public static IEndpointRouteBuilder MapReorderInventoryItem(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{itemId:int}/reorder", Handle)
               .WithName("ReorderInventoryItem")
               .Produces<InventoryItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<InventoryItemResponse>, NotFound>> Handle(
            int householdId,
            int inventoryId,
            int itemId,
            ReorderItemRequest request,
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

            var result = inventory.ReorderItem(itemId, request.AfterId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"ReorderInventoryItem cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(InventoryItemResponse.From(result.Value));
        }
    }
}
