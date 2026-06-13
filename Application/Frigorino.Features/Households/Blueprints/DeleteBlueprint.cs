using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public static class DeleteBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{blueprintId:int}", Handle)
               .WithName("DeleteBlueprint")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
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

            var blueprint = await db.SortBlueprints
                .FirstOrDefaultAsync(b => b.Id == blueprintId && b.HouseholdId == householdId && b.IsActive, ct);
            if (blueprint is null)
            {
                return TypedResults.NotFound();
            }

            blueprint.SoftDelete();

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
