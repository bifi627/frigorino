using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists
{
    public sealed record UpdateListRequest(string Name, string? Description);

    public static class UpdateListEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateList(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{listId:int}", Handle)
               .WithName("UpdateList")
               .Produces<ListResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<ListResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            int listId,
            UpdateListRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.CreatedByUser)
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);

            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var result = list.Update(currentUser.UserId, membership.Role, request.Name, request.Description);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is AccessDeniedError)
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            var response = ListResponse.From(
                list,
                list.CreatedByUser,
                list.ListItems.Count(i => i.IsActive && !i.Status),
                list.ListItems.Count(i => i.IsActive && i.Status));
            return TypedResults.Ok(response);
        }
    }
}
