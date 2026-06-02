using Frigorino.Domain.Entities;
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
    // Placeholder: notification prefs moved to per-user; retained for future household config.
    public sealed record UpdateInventorySettingsRequest();

    public static class UpdateInventorySettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateInventorySettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("", Handle)
               .WithName("UpdateInventorySettings")
               .Produces<InventorySettingsResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<Ok<InventorySettingsResponse>, NotFound, ForbidHttpResult>> Handle(
            int householdId,
            int inventoryId,
            UpdateInventorySettingsRequest request,
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
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            if (!inventory.CanBeManagedBy(currentUser.UserId, membership.Role))
            {
                return TypedResults.Forbid();
            }

            var settings = await db.InventorySettings
                .FirstOrDefaultAsync(s => s.InventoryId == inventoryId, ct);

            if (settings is null)
            {
                settings = InventorySettings.Create(inventoryId);
                db.InventorySettings.Add(settings);
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new InventorySettingsResponse());
        }
    }
}
