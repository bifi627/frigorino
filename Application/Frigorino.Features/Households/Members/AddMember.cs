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

            var household = await db.Households
                .Include(h => h.UserHouseholds)
                .FirstOrDefaultAsync(h => h.Id == householdId && h.IsActive, ct);

            if (household is null)
            {
                return TypedResults.NotFound();
            }

            var role = request.Role ?? HouseholdRole.Member;
            var result = household.AddMember(currentUser.UserId, targetUser.ExternalId, role);
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
