// Reference vertical slice — every future slice in Frigorino.Features mirrors these rules:
//  1. One slice = one file (request DTO + response DTO + endpoint registration + handler).
//  2. DTOs are sealed records.
//  3. Response DTOs expose `public static XxxResponse From(EntityType e, ...)` factory methods. No mapping libraries.
//  4. The handler is a `private static` method on the endpoint class. No separate Handler class, no MediatR.
//  5. Validation lives in the domain factory and returns `Result<T>`. Failures carry `Error`s with `WithMetadata("Property", ...)`.
//     The endpoint never re-validates; on failure it calls `ToValidationProblem()`.
//  6. No thrown exceptions for expected failures. Exceptions are reserved for genuine bugs / infrastructure faults.
//  7. Aim for one `SaveChangesAsync` per slice via EF navigation collections.
//  8. `CancellationToken` is always passed and threaded into `SaveChangesAsync(ct)`.
//  9. Use `TypedResults` (not `Results`). Endpoint return type is `Results<TSuccess, ValidationProblem>` (or other unions).
// 10. `RequireAuthorization()` on protected endpoints.
// 11. Wire format stays consistent: enums serialize as integers (no `JsonStringEnumConverter` is registered).
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Households
{
    public sealed record CreateHouseholdRequest(string Name, string? Description);

    public sealed record HouseholdResponse(
        int Id,
        string Name,
        string? Description,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string CreatedByUserId,
        HouseholdRole CurrentUserRole)
    {
        public static HouseholdResponse From(Household h, HouseholdRole role)
        {
            return new HouseholdResponse(
                h.Id, h.Name, h.Description, h.CreatedAt, h.UpdatedAt, h.CreatedByUserId, role);
        }
    }

    public static class CreateHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapCreateHousehold(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/household", Handle)
               .RequireAuthorization()
               .WithName("CreateHousehold")
               .WithTags("Households")
               .Produces<HouseholdResponse>(StatusCodes.Status201Created)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<HouseholdResponse>, ValidationProblem>> Handle(
            CreateHouseholdRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var creation = Household.Create(request.Name, request.Description, currentUser.UserId);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var household = creation.Value;
            db.Households.Add(household);
            await db.SaveChangesAsync(ct);

            var response = HouseholdResponse.From(household, HouseholdRole.Owner);
            return TypedResults.Created($"/api/household/{household.Id}", response);
        }
    }
}
