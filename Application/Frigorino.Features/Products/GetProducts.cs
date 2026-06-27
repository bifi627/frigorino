using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Products
{
    public static class GetProductsEndpoint
    {
        public static IEndpointRouteBuilder MapGetProducts(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetProducts")
               .Produces<List<ProductCatalogItem>>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<List<ProductCatalogItem>>, NotFound>> Handle(
            int householdId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Inline projection — EF can't translate the Effective*/IsOverridden getters, so the
            // override-wins logic is expressed in column terms here.
            var items = await db.Products
                .Where(p => p.HouseholdId == householdId)
                .OrderBy(p => p.NormalizedName)
                .Select(p => new ProductCatalogItem(
                    p.Id,
                    p.NormalizedName,
                    p.OverrideProductCategory ?? p.ClassificationProductCategory,
                    p.OverrideExpiryHandling ?? p.ClassificationExpiryHandling,
                    p.OverrideExpiryHandling != null ? p.OverrideShelfLifeDays : p.ClassificationShelfLifeDays,
                    p.OverrideExpiryHandling != null,
                    p.ClassificationProductCategory,
                    p.ClassificationExpiryHandling,
                    p.ClassificationShelfLifeDays))
                .ToListAsync(ct);

            return TypedResults.Ok(items);
        }
    }
}
