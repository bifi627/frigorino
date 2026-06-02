using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Notifications
{
    public sealed record UpdateMyInventoryNotificationRequest(bool Enabled, int? LeadDays);

    public static class UpdateMyInventoryNotificationEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateMyInventoryNotification(this IEndpointRouteBuilder app)
        {
            app.MapPut("", Handle)
               .WithName("UpdateMyInventoryNotification")
               .Produces<MyInventoryNotificationResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        // Public static so the unit test calls it directly (repo convention — no InternalsVisibleTo).
        public static async Task<Results<Ok<MyInventoryNotificationResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int inventoryId,
            UpdateMyInventoryNotificationRequest request,
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

            var settings = await db.UserInventoryNotificationSettings
                .FirstOrDefaultAsync(s => s.InventoryId == inventoryId && s.UserId == currentUser.UserId, ct);

            if (settings is null)
            {
                settings = UserInventoryNotificationSetting.Create(currentUser.UserId, inventoryId);
                db.UserInventoryNotificationSettings.Add(settings);
            }

            settings.SetEnabled(request.Enabled);
            var result = settings.SetLeadDays(request.LeadDays);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new MyInventoryNotificationResponse(settings.Enabled, settings.LeadDays));
        }
    }
}
