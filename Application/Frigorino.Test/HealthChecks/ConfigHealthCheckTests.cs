using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Frigorino.Test.HealthChecks
{
    public class ConfigHealthCheckTests
    {
        [Fact]
        public async Task Returns_Healthy_When_All_Firebase_Fields_Populated()
        {
            var check = CreateCheck(new FirebaseSettings
            {
                AccessJson = "{ \"x\": 1 }",
                ValidIssuer = "https://issuer",
                ValidAudience = "audience",
            });

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task Returns_Unhealthy_With_Field_Name_When_AccessJson_Empty()
        {
            var check = CreateCheck(new FirebaseSettings
            {
                AccessJson = "",
                ValidIssuer = "https://issuer",
                ValidAudience = "audience",
            });

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains(nameof(FirebaseSettings.AccessJson), result.Description);
        }

        [Fact]
        public async Task Returns_Unhealthy_Naming_All_Three_When_All_Empty()
        {
            var check = CreateCheck(new FirebaseSettings
            {
                AccessJson = "",
                ValidIssuer = "",
                ValidAudience = "",
            });

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains(nameof(FirebaseSettings.AccessJson), result.Description);
            Assert.Contains(nameof(FirebaseSettings.ValidIssuer), result.Description);
            Assert.Contains(nameof(FirebaseSettings.ValidAudience), result.Description);
        }

        private static ConfigHealthCheck CreateCheck(FirebaseSettings settings)
        {
            return new ConfigHealthCheck(Options.Create(settings));
        }
    }
}
