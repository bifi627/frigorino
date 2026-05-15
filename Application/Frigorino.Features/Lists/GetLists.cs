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
    public static class GetListsEndpoint
    {
        public static IEndpointRouteBuilder MapGetLists(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetLists")
               .Produces<ListResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ListResponse[]>, NotFound>> Handle(
            int householdId,
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
                .Where(l => l.HouseholdId == householdId && l.IsActive)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new ListResponse(
                    l.Id,
                    l.Name,
                    l.Description,
                    l.HouseholdId,
                    l.CreatedAt,
                    l.UpdatedAt,
                    new ListCreatorResponse(l.CreatedByUser.ExternalId, l.CreatedByUser.Name, l.CreatedByUser.Email),
                    l.ListItems.Count(i => i.IsActive && !i.Status),
                    l.ListItems.Count(i => i.IsActive && i.Status)))
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
