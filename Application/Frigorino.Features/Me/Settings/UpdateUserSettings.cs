using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Me.Settings
{
    public sealed record UpdateUserSettingsRequest(string? Language);

    public static class UpdateUserSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateUserSettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("/settings", Handle)
               .WithName("UpdateUserSettings")
               .Produces<UserSettingsResponse>()
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<UserSettingsResponse>, ValidationProblem>> Handle(
            UpdateUserSettingsRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == currentUser.UserId, ct);

            if (settings is null)
            {
                settings = UserSettings.Create(currentUser.UserId);
                db.UserSettings.Add(settings);
            }

            var result = settings.SetLanguage(request.Language);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new UserSettingsResponse(
                settings.Language, settings.ExpiryNotificationsEnabled, settings.ExpiryLeadDays));
        }
    }
}
