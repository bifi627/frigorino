using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households
{
    public static class DeleteHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteHousehold(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/household/{id:int}", Handle)
               .RequireAuthorization()
               .WithName("DeleteHousehold")
               .WithTags("Households")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Handle(
            int id,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var household = await db.Households
                .Include(h => h.UserHouseholds)
                .FirstOrDefaultAsync(h => h.Id == id && h.IsActive, ct);

            if (household is null)
            {
                return TypedResults.NotFound();
            }

            var result = household.SoftDelete(currentUser.UserId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                throw new InvalidOperationException(
                    $"DeleteHousehold cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
