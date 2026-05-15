using Frigorino.Domain.Entities;
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
    public sealed record CreateListRequest(string Name, string? Description);

    public static class CreateListEndpoint
    {
        public static IEndpointRouteBuilder MapCreateList(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateList")
               .Produces<ListResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<ListResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            CreateListRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var creator = await db.Users.FirstAsync(u => u.ExternalId == currentUser.UserId, ct);

            var creation = List.Create(request.Name, request.Description, householdId, currentUser.UserId);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var list = creation.Value;
            list.CreatedByUser = creator;
            db.Lists.Add(list);
            await db.SaveChangesAsync(ct);

            var response = ListResponse.From(list, creator, uncheckedCount: 0, checkedCount: 0);
            return TypedResults.Created($"/api/household/{householdId}/lists/{list.Id}", response);
        }
    }
}
