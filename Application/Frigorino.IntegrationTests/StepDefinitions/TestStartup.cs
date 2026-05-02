using Microsoft.Extensions.DependencyInjection;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.StepDefinitions;

[Binding]
public class TestStartup
{
    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestUserContext>();
        services.AddScoped<ScenarioContextHolder>();
        return services;
    }
}
