using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Members
{
    public static class GetMembersEndpoint
    {
        public static IEndpointRouteBuilder MapGetMembers(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/household/{householdId:int}/members", Handle)
               .RequireAuthorization()
               .WithName("GetMembers")
               .WithTags("Members")
               .Produces<MemberResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<MemberResponse[]>, NotFound>> Handle(
            int householdId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var hasAccess = await db.UserHouseholds
                .AnyAsync(uh => uh.UserId == currentUser.UserId
                             && uh.HouseholdId == householdId
                             && uh.IsActive
                             && uh.Household.IsActive, ct);

            if (!hasAccess)
            {
                return TypedResults.NotFound();
            }

            var response = await db.UserHouseholds
                .Where(uh => uh.HouseholdId == householdId && uh.IsActive)
                .OrderByDescending(uh => uh.Role)
                .ThenBy(uh => uh.JoinedAt)
                .Select(uh => new MemberResponse(
                    uh.User.ExternalId,
                    uh.User.Name,
                    uh.User.Email ?? string.Empty,
                    uh.Role,
                    uh.JoinedAt))
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
