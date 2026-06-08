using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Items
{
    public sealed record CreateInventoryItemRequest(string Text, QuantityDto? Quantity, DateOnly? ExpiryDate);

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

            Quantity? quantity = null;
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed)
                {
                    return parsed.ToValidationProblem();
                }
                quantity = parsed.Value;
            }

            // AddItem mints a Rank by appending after the last item; a concurrent append can collide
            // on the partial unique index. RankRetry reloads fresh state and re-mints.
            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var inventory = await db.Inventories
                    .Include(i => i.InventoryItems)
                    .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
                if (inventory is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                var result = inventory.AddItem(request.Text, quantity, request.ExpiryDate);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);
                return new CreateOutcome(InventoryItemResponse.From(result.Value), NotFound: false, Problem: null);
            });

            if (outcome.NotFound)
            {
                return TypedResults.NotFound();
            }
            if (outcome.Problem is not null)
            {
                return outcome.Problem;
            }

            var response = outcome.Response!;
            return TypedResults.Created(
                $"/api/household/{householdId}/inventories/{inventoryId}/items/{response.Id}",
                response);
        }

        private sealed record CreateOutcome(InventoryItemResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
