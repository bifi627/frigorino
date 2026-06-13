using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Households.Blueprints
{
    public sealed record CreateBlueprintRequest(string Name, IReadOnlyList<ProductCategory> Categories);

    public static class CreateBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapCreateBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateBlueprint")
               .Produces<SortBlueprintResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            CreateBlueprintRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var categories = request.Categories ?? Array.Empty<ProductCategory>();
            var result = SortBlueprint.Create(householdId, request.Name ?? string.Empty, categories);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            db.SortBlueprints.Add(result.Value);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(SortBlueprintResponse.From(result.Value));
        }
    }
}
