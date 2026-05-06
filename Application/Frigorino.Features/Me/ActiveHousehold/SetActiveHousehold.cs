using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Me.ActiveHousehold
{
    public sealed record SetActiveHouseholdRequest(int HouseholdId);

    public static class SetActiveHouseholdEndpoint
    {
        public static IEndpointRouteBuilder MapSetActiveHousehold(this IEndpointRouteBuilder app)
        {
            app.MapPut("/api/me/active-household", Handle)
               .RequireAuthorization()
               .WithName("SetActiveHousehold")
               .WithTags("Me")
               .Produces<ActiveHouseholdResponse>()
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<Ok<ActiveHouseholdResponse>, ForbidHttpResult>> Handle(
            SetActiveHouseholdRequest request,
            ICurrentHouseholdService currentHousehold)
        {
            try
            {
                await currentHousehold.SetCurrentHouseholdAsync(request.HouseholdId);
            }
            catch (UnauthorizedAccessException)
            {
                return TypedResults.Forbid();
            }

            var role = await currentHousehold.GetCurrentHouseholdRoleAsync();
            return TypedResults.Ok(new ActiveHouseholdResponse(request.HouseholdId, role, true));
        }
    }
}
