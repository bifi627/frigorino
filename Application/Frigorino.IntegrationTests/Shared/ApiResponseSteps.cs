using System.Text.Json;

namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class ApiResponseSteps(ScenarioContextHolder ctx)
{
    [Then("the API response status is {int}")]
    public void ThenTheApiResponseStatusIs(int expected)
    {
        ctx.LastApiResponse.Should().NotBeNull("no API response was captured by a prior step");
        ctx.LastApiResponse!.Status.Should().Be(expected);
    }

    [Then("the API response has a validation error for {string}")]
    public async Task ThenTheApiResponseHasAValidationErrorFor(string property)
    {
        ctx.LastApiResponse.Should().NotBeNull("no API response was captured by a prior step");
        var body = await ctx.LastApiResponse!.JsonAsync();
        body.Should().NotBeNull();
        var errors = body!.Value.GetProperty("errors");
        // Dictionary keys in HttpValidationProblemDetails are preserved as-is in JSON,
        // but match case-insensitively to stay resilient to future normalization.
        var found = errors.EnumerateObject()
            .Any(p => p.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
        found.Should().BeTrue(
            $"expected a validation error keyed on '{property}', got: {errors.GetRawText()}");
    }
}
