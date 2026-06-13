using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public static class GetInventoryRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetInventoryRevision(this IEndpointRouteBuilder app)
        {
            app.MapGet("{inventoryId:int}/revision", Handle)
               .WithName("GetInventoryRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
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

            var inventory = await db.Inventories
                .Where(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive)
                .Select(i => new { i.UpdatedAt })
                .FirstOrDefaultAsync(ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var items = db.InventoryItems.Where(i => i.InventoryId == inventoryId && i.IsActive);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(inventory.UpdatedAt, maxUpdatedAt, count));
        }
    }
}
