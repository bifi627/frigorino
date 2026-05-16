using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class GetItemsEndpoint
    {
        public static IEndpointRouteBuilder MapGetItems(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetItems")
               .Produces<ListItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ListItemResponse[]>, NotFound>> Handle(
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

            var listExists = await db.Lists.AnyAsync(
                l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (!listExists)
            {
                return TypedResults.NotFound();
            }

            var response = await db.ListItems
                .Where(i => i.ListId == listId && i.IsActive)
                .OrderBy(i => i.Status)
                .ThenBy(i => i.SortOrder)
                .Select(ListItemResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
