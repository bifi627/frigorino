using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Me.Settings
{
    public static class GetUserSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapGetUserSettings(this IEndpointRouteBuilder app)
        {
            app.MapGet("/settings", Handle)
               .WithName("GetUserSettings")
               .Produces<UserSettingsResponse>();
            return app;
        }

        private static async Task<Ok<UserSettingsResponse>> Handle(
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var response = await db.UserSettings
                .Where(s => s.UserId == currentUser.UserId)
                .Select(s => new UserSettingsResponse(s.Language))
                .FirstOrDefaultAsync(ct);

            return TypedResults.Ok(response ?? new UserSettingsResponse(null));
        }
    }
}
