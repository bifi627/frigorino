using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public static class GetBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapGetBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{blueprintId:int}", Handle)
               .WithName("GetBlueprint")
               .Produces<SortBlueprintResponse>()
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

            var response = await db.SortBlueprints
                .Where(b => b.Id == blueprintId && b.HouseholdId == householdId && b.IsActive)
                .Select(b => new SortBlueprintResponse(
                    b.Id,
                    b.Name,
                    b.Categories.OrderBy(c => c.OrderIndex).Select(c => c.Category).ToList()))
                .FirstOrDefaultAsync(ct);

            if (response is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(response);
        }
    }
}
