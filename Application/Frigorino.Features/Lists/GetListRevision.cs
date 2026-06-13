using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists
{
    public static class GetListRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetListRevision(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{listId:int}/revision", Handle)
               .WithName("GetListRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
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

            var list = await db.Lists
                .Where(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive)
                .Select(l => new { l.UpdatedAt })
                .FirstOrDefaultAsync(ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            // Two cheap scalar aggregates over IX_ListItems_ListId_IsActive. Kept as two queries
            // (not a GroupBy projection) for reliable EF translation; both are sub-millisecond.
            var items = db.ListItems.Where(i => i.ListId == listId && i.IsActive);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(list.UpdatedAt, maxUpdatedAt, count));
        }
    }
}
