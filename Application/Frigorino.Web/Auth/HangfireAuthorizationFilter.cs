using Hangfire.Annotations;
using Hangfire.Dashboard;
using System.Text;

namespace Frigorino.Web.Auth
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly string _username;
        private readonly string _password;

        public HangfireAuthorizationFilter(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public bool Authorize([NotNull] DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Check if Authorization header exists
            string? authHeader = httpContext.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                // Set WWW-Authenticate header to prompt for basic auth
                httpContext.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Hangfire Dashboard\"");
                httpContext.Response.StatusCode = 401;
                return false;
            }

            try
            {
                // Extract and decode credentials
                string encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
                string credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                
                int colonIndex = credentials.IndexOf(':');
                if (colonIndex == -1)
                {
                    return false;
                }

                string username = credentials.Substring(0, colonIndex);
                string password = credentials.Substring(colonIndex + 1);

                // Validate credentials
                return username == _username && password == _password;
            }
            catch
            {
                // Invalid base64 or other parsing error
                return false;
            }
        }
    }
}