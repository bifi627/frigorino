using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Products
{
    public sealed record OverrideProductClassificationRequest(
        ProductCategory Category,
        ExpiryHandling ExpiryHandling,
        int? ShelfLifeDays);

    public static class OverrideProductClassificationEndpoint
    {
        public static IEndpointRouteBuilder MapOverrideProductClassification(this IEndpointRouteBuilder app)
        {
            app.MapPut("{productId:int}/classification", Handle)
               .WithName("OverrideProductClassification")
               .Produces<ProductCatalogItem>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<ProductCatalogItem>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            int productId,
            OverrideProductClassificationRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (!membership.Role.CanManageSettings())
            {
                return TypedResults.Forbid();
            }

            var product = await db.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.HouseholdId == householdId, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            var profile = ExpiryProfile.Create(request.ExpiryHandling, request.ShelfLifeDays);
            if (profile.IsFailed)
            {
                return profile.ToValidationProblem();
            }

            product.OverrideClassification(new ProductClassification(request.Category, profile.Value));
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(ProductCatalogItem.From(product));
        }
    }
}
