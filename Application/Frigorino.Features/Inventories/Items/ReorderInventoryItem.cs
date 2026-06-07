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

namespace Frigorino.Features.Inventories.Items
{
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

            // Reorder mints a Rank from current neighbours; a concurrent same-slot reorder can
            // collide on the partial unique index. RankRetry reloads fresh state and re-mints.
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var inventory = await db.Inventories
                    .Include(i => i.InventoryItems)
                    .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
                if (inventory is null)
                {
                    return (InventoryItemResponse?)null;
                }

                var result = inventory.ReorderItem(itemId, request.AfterId);
                if (result.IsFailed)
                {
                    var first = result.Errors[0];
                    if (first is EntityNotFoundError)
                    {
                        return null;
                    }
                    throw new InvalidOperationException(
                        $"ReorderInventoryItem cannot map error of type {first.GetType().Name}.");
                }

                await db.SaveChangesAsync(ct);
                return InventoryItemResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
