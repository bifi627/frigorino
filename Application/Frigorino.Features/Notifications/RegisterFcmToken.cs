using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Notifications
{
    public sealed record RegisterFcmTokenRequest(string Token);

    public static class RegisterFcmTokenEndpoint
    {
        public static IEndpointRouteBuilder MapRegisterFcmToken(this IEndpointRouteBuilder app)
        {
            app.MapPost("/token", Handle)
               .WithName("RegisterFcmToken");
            return app;
        }

        // Public static so the unit test calls it directly (repo convention — no InternalsVisibleTo).
        public static async Task<Ok> Handle(
            RegisterFcmTokenRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var existing = await db.FcmTokens.FirstOrDefaultAsync(t => t.Token == request.Token, ct);
            if (existing is null)
            {
                db.FcmTokens.Add(FcmToken.Create(currentUser.UserId, request.Token));
            }
            else
            {
                // Re-register: claim the token for the current user (LastSeenAt stamped on save).
                existing.UserId = currentUser.UserId;
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok();
        }
    }
}
