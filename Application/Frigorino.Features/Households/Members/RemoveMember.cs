using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Members
{
    public static class RemoveMemberEndpoint
    {
        public static IEndpointRouteBuilder MapRemoveMember(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/household/{householdId:int}/members/{userId}", Handle)
               .RequireAuthorization()
               .WithName("RemoveMember")
               .WithTags("Members")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            string userId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var household = await db.Households
                .Include(h => h.UserHouseholds)
                .FirstOrDefaultAsync(h => h.Id == householdId && h.IsActive, ct);

            if (household is null)
            {
                return TypedResults.NotFound();
            }

            var result = household.RemoveMember(currentUser.UserId, userId);
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
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
