using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Me.ActiveHousehold
{
    public static class GetActiveHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapGetActiveHousehold(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/me/active-household", Handle)
               .RequireAuthorization()
               .WithName("GetActiveHousehold")
               .WithTags("Me")
               .Produces<ActiveHouseholdResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ActiveHouseholdResponse>, NotFound>> Handle(
            ICurrentHouseholdService currentHousehold)
        {
            var householdId = await currentHousehold.GetCurrentHouseholdIdAsync();
            if (!householdId.HasValue)
            {
                return TypedResults.NotFound();
            }

            var role = await currentHousehold.GetCurrentHouseholdRoleAsync();
            return TypedResults.Ok(new ActiveHouseholdResponse(householdId.Value, role, true));
        }
    }
}
