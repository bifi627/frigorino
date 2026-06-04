using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Promote
{
    // X (one id) or Clear All (all pending ids) — resolve without writing to inventory.
    public sealed record SkipPromotionRequest(List<int> ListItemIds);

    public static class SkipPromotionEndpoint
    {
        public static IEndpointRouteBuilder MapSkipPromotion(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{listId:int}/promote/skip", Handle)
               .WithName("SkipPromotion")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
            int householdId,
            int listId,
            SkipPromotionRequest request,
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

            var now = DateTime.UtcNow;
            foreach (var itemId in request.ListItemIds ?? new List<int>())
            {
                // No-op for ids not on the list or already resolved (idempotent); never 404 a skip.
                list.ResolvePromotion(itemId, now);
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
