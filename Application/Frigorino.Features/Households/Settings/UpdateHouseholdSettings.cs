using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Settings
{
    public sealed record UpdateHouseholdSettingsRequest(int CheckedItemRetentionDays);

    public static class UpdateHouseholdSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateHouseholdSettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("", Handle)
               .WithName("UpdateHouseholdSettings")
               .Produces<HouseholdSettingsResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<HouseholdSettingsResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            UpdateHouseholdSettingsRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (!membership.Role.CanManageSettings())
            {
                return TypedResults.Forbid();
            }

            var settings = await db.HouseholdSettings
                .FirstOrDefaultAsync(s => s.HouseholdId == householdId, ct);

            if (settings is null)
            {
                settings = HouseholdSettings.Create(householdId);
                db.HouseholdSettings.Add(settings);
            }

            var result = settings.SetCheckedItemRetentionDays(request.CheckedItemRetentionDays);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new HouseholdSettingsResponse(settings.CheckedItemRetentionDays));
        }
    }
}
