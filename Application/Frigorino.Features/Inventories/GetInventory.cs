using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public static class GetInventoryEndpoint
    {
        public static IEndpointRouteBuilder MapGetInventory(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{inventoryId:int}", Handle)
               .WithName("GetInventory")
               .Produces<InventoryResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<InventoryResponse>, NotFound>> Handle(
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

            var response = await db.Inventories
                .Where(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive)
                .Select(InventoryResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            if (response is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(response);
        }
    }
}
