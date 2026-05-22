using System.Text.Json;

namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class ApiResponseSteps(ScenarioContextHolder ctx)
{
    [Then("the API response status is {int}")]
    public void ThenTheApiResponseStatusIs(int expected)
    {
        Assert.NotNull(ctx.LastApiResponse);
        Assert.Equal(expected, ctx.LastApiResponse.Status);
    }

    [Then("the API response has a validation error for {string}")]
    public async Task ThenTheApiResponseHasAValidationErrorFor(string property)
    {
        Assert.NotNull(ctx.LastApiResponse);
        var body = await ctx.LastApiResponse.JsonAsync();
        Assert.NotNull(body);
        var errors = body.Value.GetProperty("errors");
        // Dictionary keys in HttpValidationProblemDetails are preserved as-is in JSON,
        // but match case-insensitively to stay resilient to future normalization.
        var found = errors.EnumerateObject()
            .Any(p => p.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
        Assert.True(found,
            $"expected a validation error keyed on '{property}', got: {errors.GetRawText()}");
    }
}
