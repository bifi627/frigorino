using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Promote
{
    // One row from the promote sheet: a checked candidate the user selected, with the (possibly
    // edited) quantity/expiry to write into inventory.
    public sealed record PromoteEntry(int ListItemId, QuantityDto? Quantity, DateOnly? ExpiryDate);

    public sealed record PromoteListItemsRequest(int InventoryId, List<PromoteEntry> Items);

    public sealed record PromoteListItemsResponse(int PromotedCount);

    public static class PromoteListItemsEndpoint
    {
        public static IEndpointRouteBuilder MapPromoteListItems(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{listId:int}/promote", Handle)
               .WithName("PromoteListItems")
               .Produces<PromoteListItemsResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<PromoteListItemsResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int listId,
            PromoteListItemsRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Items)] = ["At least one item is required."],
                });
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var inventory = await db.Inventories
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == request.InventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var now = DateTime.UtcNow;
            var promoted = 0;
            // Dedupe by id so a malformed payload with a repeated ListItemId can't double-promote (the resolve stamp isn't persisted until the final SaveChanges).
            foreach (var entry in request.Items.DistinctBy(e => e.ListItemId))
            {
                var sourceItem = list.ListItems.FirstOrDefault(i => i.Id == entry.ListItemId && i.IsActive);
                if (sourceItem is null)
                {
                    return TypedResults.NotFound();
                }

                // Already resolved by a racing member → skip silently (idempotent batch).
                if (sourceItem.PromotionResolvedAt is not null)
                {
                    continue;
                }

                Quantity? quantity = null;
                if (entry.Quantity is not null)
                {
                    var parsed = Quantity.Create(entry.Quantity.Value, entry.Quantity.Unit);
                    if (parsed.IsFailed)
                    {
                        return parsed.ToValidationProblem();
                    }
                    quantity = parsed.Value;
                }

                var added = inventory.AddItem(sourceItem.Text, quantity, entry.ExpiryDate);
                if (added.IsFailed)
                {
                    return added.ToValidationProblem();
                }

                var resolved = list.ResolvePromotion(sourceItem.Id, now);
                if (resolved.IsFailed)
                {
                    // ResolvePromotion only fails EntityNotFound, already excluded above.
                    return TypedResults.NotFound();
                }
                promoted++;
            }

            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(new PromoteListItemsResponse(promoted));
        }
    }
}
