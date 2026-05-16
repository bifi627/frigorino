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
    public static class DeleteItemEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteItem(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{itemId:int}", Handle)
               .WithName("DeleteItem")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
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

            var result = list.RemoveItem(itemId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"DeleteItem cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
