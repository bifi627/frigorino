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

namespace Frigorino.Features.Lists.Items
{
    // Quantity is optional. When supplied (the inventory → list re-order path), it is written
    // directly and text routing / async extraction is skipped. When null, the text is routed
    // exactly as before and the quantity (if any) is filled in by the async extraction job.
    public sealed record CreateItemRequest(string Text, string? Comment, QuantityDto? Quantity = null);

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

        public static async Task<Results<Created<ListItemResponse>, NotFound, ValidationProblem>> Handle(
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

            // AddItem mints a Rank by appending after the last unchecked item; a concurrent append
            // can collide on the partial unique index. RankRetry reloads fresh state and re-mints.
            async Task<CreateOutcome> SaveItemAsync(string name, Quantity? quantity, bool extractionPending)
            {
                return await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var list = await db.Lists
                        .Include(l => l.ListItems)
                        .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                    if (list is null)
                    {
                        return new CreateOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = list.AddItem(name, quantity, request.Comment);
                    if (result.IsFailed)
                    {
                        return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);

                    var resp = ListItemResponse.From(result.Value) with
                    {
                        ExtractionPending = extractionPending,
                    };
                    return new CreateOutcome(resp, NotFound: false, Problem: null);
                });
            }

            // Re-order path: a structured quantity was supplied (carried over from an inventory item).
            // Write it directly and skip routing / extraction — there is nothing to extract.
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed)
                {
                    return parsed.ToValidationProblem();
                }

                var directOutcome = await SaveItemAsync(request.Text, parsed.Value, extractionPending: false);
                if (directOutcome.NotFound)
                {
                    return TypedResults.NotFound();
                }
                if (directOutcome.Problem is not null)
                {
                    return directOutcome.Problem;
                }

                var directResponse = directOutcome.Response!;
                return TypedResults.Created(
                    $"/api/household/{householdId}/lists/{listId}/items/{directResponse.Id}",
                    directResponse);
            }

            var analysis = ItemTextRouter.Analyze(request.Text);

            // The item is created with no quantity; if the text needs extraction the async LLM job
            // fills in the quantity (and strips the name) afterwards.
            var outcome = await SaveItemAsync(
                analysis.CleanName,
                quantity: null,
                extractionPending: analysis.Route == ItemTextRoute.NeedsExtraction);

            if (outcome.NotFound)
            {
                return TypedResults.NotFound();
            }
            if (outcome.Problem is not null)
            {
                return outcome.Problem;
            }

            var response = outcome.Response!;
            // Tell the client whether an async extraction was enqueued so its poll keys off this signal.
            quantityTrigger.OnItemRouted(householdId, listId, response.Id, analysis);

            return TypedResults.Created(
                $"/api/household/{householdId}/lists/{listId}/items/{response.Id}",
                response);
        }

        private sealed record CreateOutcome(ListItemResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
