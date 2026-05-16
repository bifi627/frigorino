using Frigorino.Infrastructure.Auth;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Frigorino.Infrastructure.HealthChecks
{
    public sealed class ConfigHealthCheck : IHealthCheck
    {
        private readonly FirebaseSettings _firebase;

        public ConfigHealthCheck(IOptions<FirebaseSettings> firebase)
        {
            _firebase = firebase.Value;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var empties = new List<string>();
            if (string.IsNullOrWhiteSpace(_firebase.AccessJson))
            {
                empties.Add(nameof(FirebaseSettings.AccessJson));
            }
            if (string.IsNullOrWhiteSpace(_firebase.ValidIssuer))
            {
                empties.Add(nameof(FirebaseSettings.ValidIssuer));
            }
            if (string.IsNullOrWhiteSpace(_firebase.ValidAudience))
            {
                empties.Add(nameof(FirebaseSettings.ValidAudience));
            }

            if (empties.Count > 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"FirebaseSettings missing: {string.Join(", ", empties)}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
