using Frigorino.Domain.Entities;
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
    public sealed record CreateInventoryRequest(string Name, string? Description);

    public static class CreateInventoryEndpoint
    {
        public static IEndpointRouteBuilder MapCreateInventory(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateInventory")
               .Produces<InventoryResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<InventoryResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            CreateInventoryRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var creator = await db.Users.FirstAsync(u => u.ExternalId == currentUser.UserId, ct);

            var creation = Inventory.Create(request.Name, request.Description, householdId, currentUser.UserId);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var inventory = creation.Value;
            inventory.CreatedByUser = creator;
            db.Inventories.Add(inventory);
            await db.SaveChangesAsync(ct);

            var response = InventoryResponse.From(inventory, creator, totalItems: 0, expiringItems: 0);
            return TypedResults.Created($"/api/household/{householdId}/inventories/{inventory.Id}", response);
        }
    }
}
