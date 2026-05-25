using System.Security.Claims;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Frigorino.Web.Hangfire
{
    // Development (incl. dev-up bypass) -> open, so the local dashboard is frictionless.
    // Otherwise require an authenticated principal whose email claim equals Hangfire:AdminEmail.
    // The Firebase token reaches /hangfire requests via the hf_dashboard_token cookie shim in
    // FirebaseAuth.OnMessageReceived. Fail closed when no admin email is configured.
    public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
    {
        private readonly IHostEnvironment _environment;
        private readonly string? _adminEmail;

        public HangfireDashboardAuthFilter(IHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _adminEmail = configuration["Hangfire:AdminEmail"];
        }

        public bool Authorize(DashboardContext context)
        {
            if (_environment.IsDevelopment())
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_adminEmail))
            {
                return false;
            }

            var user = context.GetHttpContext().User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            return string.Equals(email, _adminEmail, StringComparison.OrdinalIgnoreCase);
        }
    }
}
