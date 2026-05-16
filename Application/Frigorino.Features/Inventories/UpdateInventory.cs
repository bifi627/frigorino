using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public sealed record UpdateInventoryRequest(string Name, string? Description);

    public static class UpdateInventoryEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateInventory(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{inventoryId:int}", Handle)
               .WithName("UpdateInventory")
               .Produces<InventoryResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<InventoryResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            int inventoryId,
            UpdateInventoryRequest request,
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
                .Include(i => i.CreatedByUser)
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);

            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var result = inventory.Update(currentUser.UserId, membership.Role, request.Name, request.Description);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            var now = DateTime.UtcNow.AddDays(InventoryResponse.ExpiringWithinDays);
            var response = InventoryResponse.From(
                inventory,
                inventory.CreatedByUser,
                inventory.InventoryItems.Count(x => x.IsActive),
                inventory.InventoryItems.Count(x => x.IsActive && x.ExpiryDate.HasValue && x.ExpiryDate.Value <= now));
            return TypedResults.Ok(response);
        }
    }
}
