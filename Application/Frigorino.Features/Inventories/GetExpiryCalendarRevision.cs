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
    public static class GetExpiryCalendarRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetExpiryCalendarRevision(this IEndpointRouteBuilder app)
        {
            // Literal "calendar/revision" — the int constraint on the sibling "{inventoryId:int}/revision"
            // route keeps "calendar" from colliding, same as the existing GetExpiryCalendar route.
            app.MapGet("calendar/revision", Handle)
               .WithName("GetExpiryCalendarRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
            int householdId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Scoped to the EXACT filter the calendar query uses — so a non-perishable item edit
            // (ExpiryDate == null) does not move the token. No parent row → parentUpdatedAt null.
            var items = db.InventoryItems.Where(i => i.IsActive
                && i.ExpiryDate != null
                && i.Inventory.IsActive
                && i.Inventory.HouseholdId == householdId);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(null, maxUpdatedAt, count));
        }
    }
}
