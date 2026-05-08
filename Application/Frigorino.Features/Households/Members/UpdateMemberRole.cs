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
    public sealed record UpdateMemberRoleRequest(HouseholdRole Role);

    public static class UpdateMemberRoleEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateMemberRole(this IEndpointRouteBuilder app)
        {
            app.MapPut("/api/household/{householdId:int}/members/{userId}/role", Handle)
               .RequireAuthorization()
               .WithName("UpdateMemberRole")
               .WithTags("Members")
               .Produces<MemberResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<MemberResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            string userId,
            UpdateMemberRoleRequest request,
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

            if (callerMembership.Role == HouseholdRole.Member)
            {
                return TypedResults.Forbid();
            }

            var targetMembership = await db.UserHouseholds
                .Include(uh => uh.User)
                .FirstOrDefaultAsync(uh => uh.UserId == userId
                                        && uh.HouseholdId == householdId
                                        && uh.IsActive, ct);

            if (targetMembership is null)
            {
                return TypedResults.NotFound();
            }

            if (targetMembership.Role == HouseholdRole.Owner
                && callerMembership.Role != HouseholdRole.Owner)
            {
                return TypedResults.Forbid();
            }

            var demotingSelfFromOwner = callerMembership.UserId == targetMembership.UserId
                && targetMembership.Role == HouseholdRole.Owner
                && request.Role != HouseholdRole.Owner;

            if (demotingSelfFromOwner)
            {
                var ownerCount = await db.UserHouseholds
                    .CountAsync(uh => uh.HouseholdId == householdId
                                   && uh.Role == HouseholdRole.Owner
                                   && uh.IsActive, ct);

                if (ownerCount <= 1)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["role"] = ["Cannot remove the last owner."],
                    });
                }
            }

            targetMembership.Role = request.Role;
            await db.SaveChangesAsync(ct);

            var response = new MemberResponse(
                targetMembership.User.ExternalId,
                targetMembership.User.Name,
                targetMembership.User.Email ?? string.Empty,
                targetMembership.Role,
                targetMembership.JoinedAt);

            return TypedResults.Ok(response);
        }
    }
}
