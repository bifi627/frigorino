using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.CurrentHousehold
{
    public static class GetCurrentHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapGetCurrentHousehold(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/currenthousehold", Handle)
               .RequireAuthorization()
               .WithName("GetCurrentHousehold")
               .WithTags("CurrentHousehold")
               .Produces<CurrentHouseholdResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<CurrentHouseholdResponse>, NotFound>> Handle(
            ICurrentHouseholdService currentHousehold)
        {
            var householdId = await currentHousehold.GetCurrentHouseholdIdAsync();
            if (!householdId.HasValue)
            {
                return TypedResults.NotFound();
            }

            var role = await currentHousehold.GetCurrentHouseholdRoleAsync();
            return TypedResults.Ok(new CurrentHouseholdResponse(householdId.Value, role, true));
        }
    }
}
