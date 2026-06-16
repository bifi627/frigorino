using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.CopyToList
{
    // One ingredient kept in the copy sheet, with the (scaled/edited) quantity to write. Text and
    // Comment are read from the recipe item server-side; only the quantity is client-supplied.
    // A null Quantity is legitimate — recipe items can be text-only ("salt to taste").
    public sealed record CopyEntry(int RecipeItemId, QuantityDto? Quantity);

    public sealed record CopyRecipeToListRequest(int TargetListId, List<CopyEntry> Items);

    public sealed record CopyRecipeToListResponse(int CopiedCount);

    public static class CopyRecipeToListEndpoint
    {
        public static IEndpointRouteBuilder MapCopyRecipeToList(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{recipeId:int}/copy-to-list", Handle)
               .WithName("CopyRecipeToList")
               .Produces<CopyRecipeToListResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<CopyRecipeToListResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int recipeId,
            CopyRecipeToListRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IProductClassificationTrigger classificationTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Items)] = ["At least one item is required."],
                });
            }

            var recipe = await db.Recipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == request.TargetListId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var copied = 0;
            // Clean names to classify after the save commits. Classification (not quantity
            // extraction) is what lets a later check-off offer promote-to-inventory: ToggleItemStatus
            // looks up the Product by normalized name, and recipe items never create Product rows
            // (ExtractRecipeQuantityJob is deliberately classification-free), so without this the
            // copied item has no catalog row and the promote bar never appears.
            var namesToClassify = new List<string>();
            // Dedupe by id so a malformed payload with a repeated RecipeItemId copies once.
            foreach (var entry in request.Items.DistinctBy(e => e.RecipeItemId))
            {
                var source = recipe.Items.FirstOrDefault(i => i.Id == entry.RecipeItemId && i.IsActive);
                // Item deleted between sheet-load and submit → skip silently (copiedCount reflects reality).
                if (source is null)
                {
                    continue;
                }

                Quantity? quantity = null;
                if (entry.Quantity is not null)
                {
                    var parsed = Quantity.Create(entry.Quantity.Value, entry.Quantity.Unit);
                    if (parsed.IsFailed)
                    {
                        return parsed.ToValidationProblem();
                    }
                    quantity = parsed.Value;
                }

                // Same aggregate method CreateItem uses: lands unchecked, Type=Text, appended.
                var added = list.AddItem(source.Text, quantity, source.Comment);
                if (added.IsFailed)
                {
                    return added.ToValidationProblem();
                }
                copied++;

                // The structured quantity is already supplied → skip quantity extraction (it would
                // re-parse and could clobber it, mirroring CreateItem's re-order path). Still route the
                // text so junk/URLs are screened out exactly like the normal create path.
                var analysis = ItemTextRouter.Analyze(source.Text);
                if (analysis.Route != ItemTextRoute.SkipAi)
                {
                    namesToClassify.Add(analysis.CleanName);
                }
            }

            await db.SaveChangesAsync(ct);

            // Fire-and-forget after the commit (enabled trigger enqueues classify; disabled is a no-op).
            foreach (var name in namesToClassify)
            {
                classificationTrigger.OnProductReferenced(householdId, name);
            }

            return TypedResults.Ok(new CopyRecipeToListResponse(copied));
        }
    }
}
