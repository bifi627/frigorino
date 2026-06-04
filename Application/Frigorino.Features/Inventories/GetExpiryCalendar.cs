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
    public static class GetExpiryCalendarEndpoint
    {
        public static IEndpointRouteBuilder MapGetExpiryCalendar(this IEndpointRouteBuilder app)
        {
            // Collection-level view over ALL of the household's inventories — note the literal
            // "calendar" segment, NOT "{inventoryId}/calendar". The int route constraint on the
            // sibling "{inventoryId:int}" routes keeps "calendar" from colliding with them.
            app.MapGet("calendar", Handle)
               .WithName("GetExpiryCalendar")
               .Produces<ExpiryCalendarItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ExpiryCalendarItemResponse[]>, NotFound>> Handle(
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

            var response = await db.InventoryItems
                .Where(i => i.IsActive
                    && i.ExpiryDate != null
                    && i.Inventory.IsActive
                    && i.Inventory.HouseholdId == householdId)
                .OrderBy(i => i.ExpiryDate)
                .Select(ExpiryCalendarItemResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
