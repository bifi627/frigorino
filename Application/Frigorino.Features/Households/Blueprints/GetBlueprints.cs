using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public static class GetBlueprintsEndpoint
    {
        public static IEndpointRouteBuilder MapGetBlueprints(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetBlueprints")
               .Produces<SortBlueprintResponse[]>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse[]>, NotFound>> Handle(
            int householdId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Lazy seed: the very first read for a household that has NEVER had a blueprint mints
            // the starter so the feature works on first tap (existing households get it too). The
            // guard counts soft-deleted rows too, so deleting every blueprint is a valid end state
            // and the delete-then-undo flow can't race a re-seed into a duplicate default.
            var everExisted = await db.SortBlueprints.AnyAsync(b => b.HouseholdId == householdId, ct);
            if (!everExisted)
            {
                db.SortBlueprints.Add(SortBlueprint.CreateDefault(householdId));
                await db.SaveChangesAsync(ct);
            }

            var response = await db.SortBlueprints
                .Where(b => b.HouseholdId == householdId && b.IsActive)
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.Id)
                .Select(b => new SortBlueprintResponse(
                    b.Id,
                    b.Name,
                    b.Categories.OrderBy(c => c.OrderIndex).Select(c => c.Category).ToList()))
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
