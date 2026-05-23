using System.Text.RegularExpressions;

namespace Frigorino.IntegrationTests.Slices.Smoke;

[Binding]
public class StaticAssetsSteps(ScenarioContextHolder ctx)
{
    private static readonly Regex _hashedAssetPattern =
        new("""/assets/[A-Za-z0-9._-]+?\.js""", RegexOptions.Compiled);

    private IAPIResponse? _response;

    [When("I request a hashed SPA asset with Accept-Encoding {string}")]
    public async Task WhenIRequestAHashedSpaAssetWithAcceptEncoding(string acceptEncoding)
    {
        var assetPath = await DiscoverHashedAssetPathAsync();
        _response = await ctx.BrowserContext.APIRequest.GetAsync(assetPath, new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Accept-Encoding"] = acceptEncoding },
        });
    }

    [When("I request {string}")]
    public async Task WhenIRequest(string path)
    {
        _response = await ctx.BrowserContext.APIRequest.GetAsync(path);
    }

    [Then("the static-asset response status is {int}")]
    public void ThenTheStaticAssetResponseStatusIs(int expected)
    {
        Assert.NotNull(_response);
        Assert.Equal(expected, _response.Status);
    }

    [Then("the static-asset response header {string} equals {string}")]
    public void ThenTheStaticAssetResponseHeaderEquals(string name, string expected)
    {
        Assert.NotNull(_response);
        var actual = GetHeader(name);
        Assert.True(actual is not null, $"response was missing the {name} header");
        Assert.Equal(expected, actual);
    }

    [Then("the static-asset response header {string} contains {string}")]
    public void ThenTheStaticAssetResponseHeaderContains(string name, string expected)
    {
        Assert.NotNull(_response);
        var actual = GetHeader(name);
        Assert.True(actual is not null, $"response was missing the {name} header");
        Assert.Contains(expected, actual);
    }

    [Then("the static-asset response has no {string} header")]
    public void ThenTheStaticAssetResponseHasNoHeader(string name)
    {
        Assert.NotNull(_response);
        Assert.Null(GetHeader(name));
    }

    private string? GetHeader(string name)
    {
        // Playwright's IAPIResponse.Headers is case-insensitive in practice but we match
        // explicitly to stay robust against any underlying dictionary semantics.
        foreach (var kvp in _response!.Headers)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return null;
    }

    private async Task<string> DiscoverHashedAssetPathAsync()
    {
        // Fetch the SPA shell uncompressed so we can scan it as text, then pick the first
        // /assets/*.js reference. Vite-hashed filenames guarantee a unique path per build.
        var indexResponse = await ctx.BrowserContext.APIRequest.GetAsync("/", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        Assert.Equal(200, indexResponse.Status);

        var bodyBytes = await indexResponse.BodyAsync();
        var body = System.Text.Encoding.UTF8.GetString(bodyBytes);
        var match = _hashedAssetPattern.Match(body);
        Assert.True(match.Success,
            $"expected /assets/*.js reference in index.html — got body:\n{body[..Math.Min(body.Length, 500)]}");
        return match.Value;
    }
}
