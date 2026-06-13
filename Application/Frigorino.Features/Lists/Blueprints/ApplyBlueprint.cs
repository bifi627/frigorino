using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Lists.Items;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Blueprints
{
    public sealed record ApplyBlueprintRequest(int BlueprintId);

    public static class ApplyBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapApplyBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{listId:int}/apply-blueprint", Handle)
               .WithName("ApplyBlueprint")
               .Produces<ListItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        // Any active member may apply (it is just a reorder of a shared list). Reads the unchecked
        // items, resolves each item's category from the Product catalog, computes the blueprint
        // order, and bulk re-ranks via List.ApplyOrder. RankRetry guards a concurrent append/reorder
        // racing the re-rank on the partial unique index.
        private static async Task<Results<Ok<ListItemResponse[]>, NotFound>> Handle(
            int householdId,
            int listId,
            ApplyBlueprintRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var list = await db.Lists
                    .Include(l => l.ListItems)
                    .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                if (list is null)
                {
                    return (ListItemResponse[]?)null;
                }

                var blueprint = await db.SortBlueprints
                    .Include(b => b.Categories)
                    .FirstOrDefaultAsync(b => b.Id == request.BlueprintId && b.HouseholdId == householdId && b.IsActive, ct);
                if (blueprint is null)
                {
                    return null;
                }

                var uncheckedItems = list.ListItems
                    .Where(i => i.IsActive && !i.Status)
                    .ToList();

                // Resolve each item's effective category by normalized-name lookup on Product.
                var normalizedByItemId = uncheckedItems.ToDictionary(
                    i => i.Id,
                    i => ProductName.Normalize(i.Text));
                var names = normalizedByItemId.Values
                    .Where(n => n.Length > 0)
                    .Distinct()
                    .ToList();
                var products = await db.Products
                    .Where(p => p.HouseholdId == householdId && names.Contains(p.NormalizedName))
                    .ToListAsync(ct);
                var categoryByName = products.ToDictionary(p => p.NormalizedName, p => p.EffectiveCategory);
                var categoryByItemId = normalizedByItemId.ToDictionary(
                    kv => kv.Key,
                    kv => categoryByName.TryGetValue(kv.Value, out var cat) ? cat : ProductCategory.Unknown);

                var orderedIds = BlueprintSorter.OrderUncheckedItemIds(
                    uncheckedItems, categoryByItemId, blueprint.OrderedCategories());

                var applyResult = list.ApplyOrder(orderedIds);
                if (applyResult.IsFailed)
                {
                    // The id set is built from the same loaded aggregate, so this only trips on a
                    // genuine bug — surface it rather than silently 404.
                    throw new InvalidOperationException(
                        $"ApplyOrder failed unexpectedly: {applyResult.Errors[0].Message}");
                }

                await db.SaveChangesAsync(ct);

                return list.ListItems
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.Status)
                    .ThenBy(i => i.Rank, StringComparer.Ordinal)
                    .ThenBy(i => i.Id)
                    .Select(ListItemResponse.From)
                    .ToArray();
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
