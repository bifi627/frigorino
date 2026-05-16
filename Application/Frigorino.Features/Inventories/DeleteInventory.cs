using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public static class DeleteInventoryEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteInventory(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{inventoryId:int}", Handle)
               .WithName("DeleteInventory")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Handle(
            int householdId,
            int inventoryId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var inventory = await db.Inventories
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);

            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var result = inventory.SoftDelete(currentUser.UserId, membership.Role);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                throw new InvalidOperationException(
                    $"DeleteInventory cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
