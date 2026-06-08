using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
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

            // Restore reactivates the item with its ORIGINAL rank — preserving its undo position is
            // the whole point. That rank is normally still free. But if a live item took the slot
            // while this one was deleted, the partial unique index rejects it; on that retry we
            // re-mint to the end of the section so the restore still succeeds.
            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var list = await db.Lists
                    .Include(l => l.ListItems)
                    .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                if (list is null)
                {
                    return (ListItemResponse?)null;
                }

                var result = list.RestoreItem(itemId);
                if (result.IsFailed)
                {
                    var first = result.Errors[0];
                    if (first is EntityNotFoundError)
                    {
                        return null;
                    }
                    throw new InvalidOperationException(
                        $"RestoreItem cannot map error of type {first.GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    list.ReplaceRestoredItemRank(itemId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return ListItemResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
