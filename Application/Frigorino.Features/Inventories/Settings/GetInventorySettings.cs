using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Settings
{
    public static class GetInventorySettingsEndpoint
    {
        public static IEndpointRouteBuilder MapGetInventorySettings(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetInventorySettings")
               .Produces<InventorySettingsResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<InventorySettingsResponse>, NotFound>> Handle(
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

            var inventoryExists = await db.Inventories
                .AnyAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (!inventoryExists)
            {
                return TypedResults.NotFound();
            }

            var response = await db.InventorySettings
                .Where(s => s.InventoryId == inventoryId)
                .Select(s => new InventorySettingsResponse(s.ExpiryNotificationsEnabled, s.ExpiryLeadDays))
                .FirstOrDefaultAsync(ct);

            // No row ⇒ enabled by default, inherit lead-days.
            return TypedResults.Ok(response ?? new InventorySettingsResponse(true, null));
        }
    }
}
