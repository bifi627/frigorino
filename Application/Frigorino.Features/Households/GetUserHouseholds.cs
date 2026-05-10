using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households
{
    public static class GetUserHouseholdsEndpoint
    {
        public static IEndpointRouteBuilder MapGetUserHouseholds(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetUserHouseholds")
               .Produces<HouseholdResponse[]>(StatusCodes.Status200OK);
            return app;
        }

        private static async Task<Ok<HouseholdResponse[]>> Handle(
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var response = await db.UserHouseholds
                .Where(uh => uh.UserId == currentUser.UserId && uh.IsActive && uh.Household.IsActive)
                .Select(uh => new HouseholdResponse(
                    uh.Household.Id,
                    uh.Household.Name,
                    uh.Household.Description,
                    uh.Household.CreatedAt,
                    uh.Household.UpdatedAt,
                    uh.Household.CreatedByUserId,
                    uh.Role))
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
