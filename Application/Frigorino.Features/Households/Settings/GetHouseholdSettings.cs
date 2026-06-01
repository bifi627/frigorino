using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Settings
{
    public static class GetHouseholdSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapGetHouseholdSettings(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetHouseholdSettings")
               .Produces<HouseholdSettingsResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<HouseholdSettingsResponse>, NotFound>> Handle(
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

            var response = await db.HouseholdSettings
                .Where(s => s.HouseholdId == householdId)
                .Select(s => new HouseholdSettingsResponse(s.CheckedItemRetentionDays))
                .FirstOrDefaultAsync(ct);

            return TypedResults.Ok(response
                ?? new HouseholdSettingsResponse(HouseholdSettings.DefaultCheckedItemRetentionDays));
        }
    }
}
