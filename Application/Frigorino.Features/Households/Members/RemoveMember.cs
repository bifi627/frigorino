using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Members
{
    public static class RemoveMemberEndpoint
    {
        public static IEndpointRouteBuilder MapRemoveMember(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/household/{householdId:int}/members/{userId}", Handle)
               .RequireAuthorization()
               .WithName("RemoveMember")
               .WithTags("Members")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            string userId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var callerMembership = await db.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == currentUser.UserId
                                        && uh.HouseholdId == householdId
                                        && uh.IsActive
                                        && uh.Household.IsActive, ct);

            if (callerMembership is null)
            {
                return TypedResults.NotFound();
            }

            var targetMembership = await db.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == userId
                                        && uh.HouseholdId == householdId
                                        && uh.IsActive, ct);

            if (targetMembership is null)
            {
                return TypedResults.NotFound();
            }

            var isSelfRemoval = callerMembership.UserId == targetMembership.UserId;
            if (!isSelfRemoval && callerMembership.Role == HouseholdRole.Member)
            {
                return TypedResults.Forbid();
            }

            if (targetMembership.Role == HouseholdRole.Owner)
            {
                var ownerCount = await db.UserHouseholds
                    .CountAsync(uh => uh.HouseholdId == householdId
                                   && uh.Role == HouseholdRole.Owner
                                   && uh.IsActive, ct);

                if (ownerCount <= 1)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["userId"] = ["Cannot remove the last owner."],
                    });
                }
            }

            targetMembership.IsActive = false;
            await db.SaveChangesAsync(ct);

            return TypedResults.NoContent();
        }
    }
}
