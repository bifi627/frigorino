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
    public static class GetInventoryItemsEndpoint
    {
        public static IEndpointRouteBuilder MapGetInventoryItems(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetInventoryItems")
               .Produces<InventoryItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<InventoryItemResponse[]>, NotFound>> Handle(
            int householdId,
            int inventoryId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var inventoryExists = await db.Inventories.AnyAsync(
                i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (!inventoryExists)
            {
                return TypedResults.NotFound();
            }

            var response = await db.InventoryItems
                .Where(i => i.InventoryId == inventoryId && i.IsActive)
                .OrderBy(i => i.SortOrder)
                .ThenByDescending(i => i.CreatedAt)
                .Select(InventoryItemResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
