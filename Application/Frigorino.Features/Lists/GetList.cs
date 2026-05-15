using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists
{
    public static class GetListEndpoint
    {
        public static IEndpointRouteBuilder MapGetList(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{listId:int}", Handle)
               .WithName("GetList")
               .Produces<ListResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ListResponse>, NotFound>> Handle(
            int householdId,
            int listId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var response = await db.Lists
                .Where(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive)
                .Select(ListResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            if (response is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(response);
        }
    }
}
