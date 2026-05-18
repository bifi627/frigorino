using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Frigorino.IntegrationTests.Infrastructure;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Defence in depth: this handler mints a principal from request headers and must
        // never be reachable outside the IntegrationTest environment. The primary guard
        // is that `Program.cs` only wires Firebase auth when the env isn't IntegrationTest
        // and `TestWebApplicationFactory` is the only thing that registers this scheme —
        // this assert just refuses to run if either of those invariants ever drifts.
        var env = Context.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsEnvironment("IntegrationTest"))
        {
            return AuthenticateResult.Fail(
                $"TestAuthHandler invoked outside IntegrationTest environment (was '{env.EnvironmentName}').");
        }

        var userId = Request.Headers["X-Test-User"].FirstOrDefault() ?? "anonymous";
        var email = Request.Headers["X-Test-Email"].FirstOrDefault() ?? $"{userId}@test.local";
        var name = Request.Headers["X-Test-Name"].FirstOrDefault() ?? userId;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim("name", name),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        // Mirror the production OnTokenValidated user-row sync. No auth_time claim is set,
        // so UserSync falls through its cache and upserts every request — exactly what we
        // want when each scenario runs against a fresh database.
        if (userId != "anonymous")
        {
            var db = Context.RequestServices.GetRequiredService<ApplicationDbContext>();
            await UserSync.EnsureAsync(principal, db, Context.RequestAborted);
        }

        return AuthenticateResult.Success(ticket);
    }
}
