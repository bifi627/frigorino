using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
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

            var analysis = ItemTextRouter.Analyze(request.Text);

            // AddItem mints a Rank by appending after the last unchecked item; a concurrent append
            // can collide on the partial unique index. RankRetry reloads fresh state and re-mints.
            // The extraction enqueue (a side effect) runs only AFTER the save commits, so a retried
            // save never double-enqueues.
            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var list = await db.Lists
                    .Include(l => l.ListItems)
                    .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                if (list is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                // The item is created with no quantity; if the text needs extraction the async LLM job
                // fills in the quantity (and strips the name) afterwards.
                var result = list.AddItem(analysis.CleanName, null, request.Comment);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);

                // Tell the client whether an async extraction was enqueued (the only route that does is
                // NeedsExtraction) so its poll keys off this single signal rather than re-deriving a digit gate.
                var resp = ListItemResponse.From(result.Value) with
                {
                    ExtractionPending = analysis.Route == ItemTextRoute.NeedsExtraction,
                };
                return new CreateOutcome(resp, NotFound: false, Problem: null);
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
            quantityTrigger.OnItemRouted(householdId, listId, response.Id, analysis);

            return TypedResults.Created(
                $"/api/household/{householdId}/lists/{listId}/items/{response.Id}",
                response);
        }

        private sealed record CreateOutcome(ListItemResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
