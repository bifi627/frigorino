// Reference vertical slice — every future slice in Frigorino.Features mirrors these rules:
//  1. One slice = one file: request DTO + endpoint registration + handler. The response DTO lives in the
//     same file by default, but may be promoted to a folder-level file (e.g. `HouseholdResponse.cs`) when
//     it is shared across multiple read slices in the same folder. See `knowledge/Vertical_Slices.md`.
//  2. DTOs are sealed records.
//  3. No mapping libraries (no AutoMapper). Two patterns are blessed:
//     - Write slices: after the domain factory returns an entity, build the response with a static
//       `XxxResponse.From(entity, ...)` factory method.
//     - Read slices: project directly into the response DTO inside the LINQ query — the projection IS
//       the mapping, and EF translates it to SQL so only the needed columns are fetched and no entity
//       is tracked.
//  4. The handler is a `private static` method on the endpoint class. No separate Handler class, no MediatR.
//  5. Validation lives in the domain factory and returns `Result<T>`. Failures carry `Error`s with `WithMetadata("Property", ...)`.
//     The endpoint never re-validates; on failure it calls `ToValidationProblem()`.
//  6. No thrown exceptions for expected failures. Exceptions are reserved for genuine bugs / infrastructure faults.
//  7. Aim for one `SaveChangesAsync` per slice via EF navigation collections.
//  8. `CancellationToken` is always passed and threaded into `SaveChangesAsync(ct)`.
//  9. Use `TypedResults` (not `Results`). Endpoint return type is `Results<TSuccess, ValidationProblem>` (or other unions).
// 10. `RequireAuthorization()` on protected endpoints.
// 11. Wire format stays consistent: enums serialize as strings (`JsonStringEnumConverter` is globally registered in Program.cs).
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

    public static class CreateHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapCreateHousehold(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateHousehold")
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
