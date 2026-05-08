using Frigorino.Domain.Entities;
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
    public sealed record AddMemberRequest(string Email, HouseholdRole? Role);

    public static class AddMemberEndpoint
    {
        public static IEndpointRouteBuilder MapAddMember(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/household/{householdId:int}/members", Handle)
               .RequireAuthorization()
               .WithName("AddMember")
               .WithTags("Members")
               .Produces<MemberResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<MemberResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            AddMemberRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var email = request.Email?.Trim() ?? string.Empty;
            if (email.Length == 0 || !email.Contains('@'))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["A valid email address is required."],
                });
            }

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

            var lowerEmail = email.ToLowerInvariant();
            var targetUser = await db.Users
                .FirstOrDefaultAsync(u => u.Email != null
                                       && u.Email.ToLower() == lowerEmail
                                       && u.IsActive, ct);

            if (targetUser is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["No user with that email."],
                });
            }

            var role = request.Role ?? HouseholdRole.Member;
            var existing = await db.UserHouseholds
                .FirstOrDefaultAsync(uh => uh.UserId == targetUser.ExternalId
                                        && uh.HouseholdId == householdId, ct);

            UserHousehold membership;
            if (existing is not null)
            {
                if (existing.IsActive)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["email"] = ["User is already a member."],
                    });
                }

                existing.IsActive = true;
                existing.Role = role;
                existing.JoinedAt = DateTime.UtcNow;
                membership = existing;
            }
            else
            {
                var creation = UserHousehold.CreateMembership(targetUser.ExternalId, householdId, role);
                if (creation.IsFailed)
                {
                    return creation.ToValidationProblem();
                }

                membership = creation.Value;
                db.UserHouseholds.Add(membership);
            }

            await db.SaveChangesAsync(ct);

            var response = new MemberResponse(
                targetUser.ExternalId,
                targetUser.Name,
                targetUser.Email ?? string.Empty,
                membership.Role,
                membership.JoinedAt);

            return TypedResults.Created(
                $"/api/household/{householdId}/members/{targetUser.ExternalId}",
                response);
        }
    }
}
