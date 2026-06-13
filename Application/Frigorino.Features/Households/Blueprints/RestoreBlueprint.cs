using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public static class RestoreBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{blueprintId:int}/restore", Handle)
               .WithName("RestoreBlueprint")
               .Produces<SortBlueprintResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse>, NotFound>> Handle(
            int householdId,
            int blueprintId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Undo target: a previously soft-deleted blueprint. Include Categories so the response
            // carries the full walk-order without a second round-trip.
            var blueprint = await db.SortBlueprints
                .Include(b => b.Categories)
                .FirstOrDefaultAsync(b => b.Id == blueprintId && b.HouseholdId == householdId && !b.IsActive, ct);
            if (blueprint is null)
            {
                return TypedResults.NotFound();
            }

            blueprint.Restore();

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(SortBlueprintResponse.From(blueprint));
        }
    }
}
