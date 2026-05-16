using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Items
{
    public sealed record CreateInventoryItemRequest(string Text, string? Quantity, DateTime? ExpiryDate);

    public static class CreateInventoryItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateInventoryItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateInventoryItem")
               .Produces<InventoryItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<InventoryItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int inventoryId,
            CreateInventoryItemRequest request,
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
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var result = inventory.AddItem(request.Text, request.Quantity, request.ExpiryDate);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            var response = InventoryItemResponse.From(result.Value);
            return TypedResults.Created(
                $"/api/household/{householdId}/inventories/{inventoryId}/items/{result.Value.Id}",
                response);
        }
    }
}
