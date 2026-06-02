using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Notifications
{
    public sealed record MyInventoryNotificationResponse(bool Enabled, int? LeadDays);

    public static class GetMyInventoryNotificationEndpoint
    {
        public static IEndpointRouteBuilder MapGetMyInventoryNotification(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetMyInventoryNotification")
               .Produces<MyInventoryNotificationResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        // Public static so the unit test calls it directly (repo convention — no InternalsVisibleTo).
        public static async Task<Results<Ok<MyInventoryNotificationResponse>, NotFound>> Handle(
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

            var response = await db.UserInventoryNotificationSettings
                .Where(s => s.InventoryId == inventoryId && s.UserId == currentUser.UserId)
                .Select(s => new MyInventoryNotificationResponse(s.Enabled, s.LeadDays))
                .FirstOrDefaultAsync(ct);

            // No row ⇒ subscribed by default, inherit lead-days.
            return TypedResults.Ok(response ?? new MyInventoryNotificationResponse(true, null));
        }
    }
}
