using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households
{
    public static class DeleteHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteHousehold(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/household/{id:int}", Handle)
               .RequireAuthorization()
               .WithName("DeleteHousehold")
               .WithTags("Households")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Handle(
            int id,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var userHousehold = await db.UserHouseholds
                .Include(uh => uh.Household)
                    .ThenInclude(h => h.UserHouseholds)
                .FirstOrDefaultAsync(uh => uh.UserId == currentUser.UserId
                                        && uh.HouseholdId == id
                                        && uh.IsActive
                                        && uh.Household.IsActive, ct);

            if (userHousehold is null)
            {
                return TypedResults.NotFound();
            }

            if (userHousehold.Role != HouseholdRole.Owner)
            {
                return TypedResults.Forbid();
            }

            var now = DateTime.UtcNow;
            userHousehold.Household.IsActive = false;
            userHousehold.Household.UpdatedAt = now;

            foreach (var membership in userHousehold.Household.UserHouseholds)
            {
                membership.IsActive = false;
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
