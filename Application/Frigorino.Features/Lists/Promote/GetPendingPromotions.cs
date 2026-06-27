using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Households;
using Frigorino.Features.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Promote
{
    // One pending-promotion candidate for the review sheet. Projected straight from the stored
    // promotion columns — the candidacy/suggestion was captured at check time, so no Product join.
    public sealed record PendingPromotionResponse(
        int ListItemId,
        string Text,
        QuantityDto? Quantity,
        ExpiryHandling ExpiryHandling,
        DateOnly? SuggestedExpiry);

    public static class GetPendingPromotionsEndpoint
    {
        public static IEndpointRouteBuilder MapGetPendingPromotions(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{listId:int}/pending-promotions", Handle)
               .WithName("GetPendingPromotions")
               .Produces<List<PendingPromotionResponse>>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<List<PendingPromotionResponse>>, NotFound>> Handle(
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

            var listExists = await db.Lists
                .AnyAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (!listExists)
            {
                return TypedResults.NotFound();
            }

            var promoteCutoff = DateTime.UtcNow.AddDays(-ListItem.PromoteWindowDays);
            var pending = await db.ListItems
                .Where(i => i.ListId == listId
                            && i.IsActive
                            && i.Status
                            && i.PromotionExpiryHandling != null
                            && i.PromotionResolvedAt == null
                            && i.UpdatedAt >= promoteCutoff)
                .OrderBy(i => i.Rank)
                .ThenBy(i => i.Id)
                .Select(i => new PendingPromotionResponse(
                    i.Id,
                    i.Text,
                    i.QuantityValue == null
                        ? null
                        : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
                    i.PromotionExpiryHandling!.Value,
                    i.PromotionSuggestedExpiry))
                .ToListAsync(ct);

            return TypedResults.Ok(pending);
        }
    }
}
