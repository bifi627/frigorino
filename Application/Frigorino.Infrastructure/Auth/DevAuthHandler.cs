using System.Security.Claims;
using System.Text.Encodings.Web;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frigorino.Infrastructure.Auth
{
    // Development-only auth shim. Mints a fixed principal from configuration so a fresh
    // clone can hit protected endpoints without a real Firebase tenant. Wired into
    // Program.cs ONLY when env == Development AND DevAuth:Enabled == true; this handler
    // additionally refuses to issue a ticket if either invariant ever drifts (defence in
    // depth). Mirrors Frigorino.IntegrationTests.Infrastructure.TestAuthHandler — the
    // production OnTokenValidated user-row sync is replicated by calling UserSync.EnsureAsync.
    public class DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHostEnvironment env,
        IOptions<DevAuthSettings> settings)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "DevAuth";

        private readonly IHostEnvironment _env = env;
        private readonly DevAuthSettings _settings = settings.Value;

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!_env.IsDevelopment() || !_settings.Enabled)
            {
                return AuthenticateResult.Fail(
                    $"DevAuthHandler invoked outside Development env or DevAuth disabled (env='{_env.EnvironmentName}', enabled={_settings.Enabled}).");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _settings.UserId),
                new Claim(ClaimTypes.Email, _settings.Email),
                new Claim("name", _settings.Name),
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            var db = Context.RequestServices.GetRequiredService<ApplicationDbContext>();
            await UserSync.EnsureAsync(principal, db, Context.RequestAborted);

            return AuthenticateResult.Success(ticket);
        }
    }

    public class DevAuthSettings
    {
        public const string SECTION_NAME = "DevAuth";

        public bool Enabled { get; set; }
        public string UserId { get; set; } = "dev-user";
        public string Email { get; set; } = "dev@frigorino.local";
        public string Name { get; set; } = "Dev User";
    }

    public static class DevAuth
    {
        public static IServiceCollection AddDevAuth(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DevAuthSettings>(configuration.GetSection(DevAuthSettings.SECTION_NAME));
            services.AddAuthentication(DevAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
            return services;
        }
    }
}
