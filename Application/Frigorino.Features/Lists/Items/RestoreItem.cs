using Frigorino.Domain.Errors;
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
    public static class RestoreItemEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{itemId:int}/restore", Handle)
               .WithName("RestoreItem")
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

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var result = list.RestoreItem(itemId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"RestoreItem cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ListItemResponse.From(result.Value));
        }
    }
}
