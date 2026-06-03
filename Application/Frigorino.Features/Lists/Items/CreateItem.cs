using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public sealed record CreateItemRequest(string Text, string? Comment);

    public static class CreateItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateItem")
               .Produces<ListItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<ListItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int listId,
            CreateItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var analysis = ItemTextRouter.Analyze(request.Text);

            var result = list.AddItem(analysis.CleanName, analysis.Quantity, request.Comment);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            quantityTrigger.OnItemRouted(householdId, listId, result.Value.Id, analysis);

            // Tell the client whether an async extraction was enqueued (the only route that does is
            // NeedsExtraction) so its poll keys off this single signal rather than re-deriving a digit gate.
            var response = ListItemResponse.From(result.Value) with
            {
                ExtractionPending = analysis.Route == ItemTextRoute.NeedsExtraction,
            };
            return TypedResults.Created(
                $"/api/household/{householdId}/lists/{listId}/items/{result.Value.Id}",
                response);
        }
    }
}
