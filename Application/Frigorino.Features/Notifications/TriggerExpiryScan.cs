using System.Security.Cryptography;
using System.Text;
using Frigorino.Infrastructure.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Frigorino.Features.Notifications
{
    // Constant-time comparison of the trigger key, isolated so it is unit-testable.
    public static class MaintenanceKey
    {
        public static bool Matches(string? provided, string expected)
        {
            if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            var a = Encoding.UTF8.GetBytes(provided);
            var b = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }

    public static class TriggerExpiryScanEndpoint
    {
        private const string HeaderName = "X-Maintenance-Key";

        public static IEndpointRouteBuilder MapTriggerExpiryScan(this IEndpointRouteBuilder app)
        {
            // Machine-to-machine: not under the user-auth group, hidden from the OpenAPI client,
            // guarded by the trigger key. A wrong/missing key returns 404 (non-discoverable).
            app.MapPost("/internal/expiry-scan", Handle)
               .ExcludeFromDescription()
               .AllowAnonymous();
            return app;
        }

        private static async Task<Results<Ok, NotFound>> Handle(
            HttpRequest request,
            IOptions<MaintenanceSettings> settings,
            ExpiryNotificationScan scan,
            CancellationToken ct)
        {
            var provided = request.Headers[HeaderName].ToString();
            if (!MaintenanceKey.Matches(provided, settings.Value.TriggerToken))
            {
                return TypedResults.NotFound();
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await scan.RunAsync(today, ct);
            return TypedResults.Ok();
        }
    }
}
