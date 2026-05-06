using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.CurrentHousehold
{
    public static class SetCurrentHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapSetCurrentHousehold(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/currenthousehold/{householdId:int}", Handle)
               .RequireAuthorization()
               .WithName("SetCurrentHousehold")
               .WithTags("CurrentHousehold")
               .Produces<CurrentHouseholdResponse>()
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<Ok<CurrentHouseholdResponse>, ForbidHttpResult>> Handle(
            int householdId,
            ICurrentHouseholdService currentHousehold)
        {
            try
            {
                await currentHousehold.SetCurrentHouseholdAsync(householdId);
            }
            catch (UnauthorizedAccessException)
            {
                return TypedResults.Forbid();
            }

            var role = await currentHousehold.GetCurrentHouseholdRoleAsync();
            return TypedResults.Ok(new CurrentHouseholdResponse(householdId, role, true));
        }
    }
}
