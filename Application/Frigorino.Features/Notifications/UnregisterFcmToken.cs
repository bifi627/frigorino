using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Notifications
{
    public sealed record UnregisterFcmTokenRequest(string Token);

    public static class UnregisterFcmTokenEndpoint
    {
        public static IEndpointRouteBuilder MapUnregisterFcmToken(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/token", Handle)
               .WithName("UnregisterFcmToken")
               .Produces(StatusCodes.Status204NoContent);
            return app;
        }

        // load-then-Remove (not ExecuteDeleteAsync): the EF InMemory provider used by the unit
        // test does not support ExecuteDeleteAsync, and the row count here is tiny.
        public static async Task<NoContent> Handle(
            UnregisterFcmTokenRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var rows = await db.FcmTokens
                .Where(t => t.Token == request.Token && t.UserId == currentUser.UserId)
                .ToListAsync(ct);
            db.FcmTokens.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
