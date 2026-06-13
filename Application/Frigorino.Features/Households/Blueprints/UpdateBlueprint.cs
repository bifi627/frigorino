using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public sealed record UpdateBlueprintRequest(string Name, IReadOnlyList<ProductCategory> Categories);

    public static class UpdateBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{blueprintId:int}", Handle)
               .WithName("UpdateBlueprint")
               .Produces<SortBlueprintResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int blueprintId,
            UpdateBlueprintRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Include Categories so the wholesale replace orphan-deletes the old rows.
            var blueprint = await db.SortBlueprints
                .Include(b => b.Categories)
                .FirstOrDefaultAsync(b => b.Id == blueprintId && b.HouseholdId == householdId && b.IsActive, ct);
            if (blueprint is null)
            {
                return TypedResults.NotFound();
            }

            var categories = request.Categories ?? Array.Empty<ProductCategory>();
            var result = blueprint.Update(request.Name ?? string.Empty, categories);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(SortBlueprintResponse.From(blueprint));
        }
    }
}
