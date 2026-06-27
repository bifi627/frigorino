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
    public static class DeleteProductEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteProduct(this IEndpointRouteBuilder app)
        {
            app.MapDelete("{productId:int}", Handle)
               .WithName("DeleteProduct")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        // Hard delete: the catalog has no soft-delete column and nothing FK-references a Product
        // (list/inventory items resolve products by normalized name, not by id). Removing the row
        // is clean — the next reference to this name re-creates and re-classifies it via
        // ClassifyProductJob.
        private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Handle(
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

            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);

            return TypedResults.NoContent();
        }
    }
}
