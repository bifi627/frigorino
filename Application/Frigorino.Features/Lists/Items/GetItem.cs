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
    public static class GetItemEndpoint
    {
        public static IEndpointRouteBuilder MapGetItem(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}", Handle)
               .WithName("GetItem")
               .Produces<ListItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ListItemResponse>, NotFound>> Handle(
            int householdId,
            int listId,
            int itemId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var response = await db.ListItems
                .Where(i => i.Id == itemId
                         && i.ListId == listId
                         && i.IsActive
                         && i.List.HouseholdId == householdId
                         && i.List.IsActive)
                .Select(ListItemResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            if (response is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(response);
        }
    }
}
