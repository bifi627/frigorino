using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class ToggleItemStatusEndpoint
    {
        public static IEndpointRouteBuilder MapToggleItemStatus(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{itemId:int}/toggle-status", Handle)
               .WithName("ToggleItemStatus")
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

            var result = list.ToggleItemStatus(itemId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"ToggleItemStatus cannot map error of type {first.GetType().Name}.");
            }

            // Only when the item is now checked DONE do we look up its product (one indexed point
            // lookup on the unique (HouseholdId, NormalizedName)) and capture a promote suggestion.
            // The suggestion is persisted on the item (shared, durable batch) AND returned in the
            // response. Un-checking clears promotion state in the aggregate (no lookup needed).
            PromoteSuggestion? suggestion = null;
            if (result.Value.Status)
            {
                var normalized = ProductName.Normalize(result.Value.Text);
                var product = await db.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.HouseholdId == householdId && p.NormalizedName == normalized, ct);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                suggestion = PromoteSuggestion.For(product, today);

                list.ApplyPromotionSuggestion(
                    result.Value.Id, suggestion?.ExpiryHandling, suggestion?.SuggestedExpiry);
            }

            await db.SaveChangesAsync(ct);

            var response = ListItemResponse.From(result.Value) with { Promote = suggestion };
            return TypedResults.Ok(response);
        }
    }
}
