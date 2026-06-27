using Frigorino.Domain.Entities;
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
    public static class ResetProductClassificationEndpoint
    {
        public static IEndpointRouteBuilder MapResetProductClassification(this IEndpointRouteBuilder app)
        {
            app.MapDelete("{productId:int}/classification", Handle)
               .WithName("ResetProductClassification")
               .Produces<ProductCatalogItem>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<Ok<ProductCatalogItem>, NotFound, ForbidHttpResult>> Handle(
            int householdId,
            int productId,
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

            product.ResetToAiClassification();
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(ProductCatalogItem.From(product));
        }
    }
}
