using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
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
            var household = await db.Households
                .Include(h => h.UserHouseholds)
                    .ThenInclude(uh => uh.User)
                .FirstOrDefaultAsync(h => h.Id == householdId && h.IsActive, ct);

            if (household is null)
            {
                return TypedResults.NotFound();
            }

            var result = household.ChangeMemberRole(currentUser.UserId, userId, request.Role);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            var membership = result.Value;
            var response = new MemberResponse(
                membership.User.ExternalId,
                membership.User.Name,
                membership.User.Email ?? string.Empty,
                membership.Role,
                membership.JoinedAt);

            return TypedResults.Ok(response);
        }
    }
}
