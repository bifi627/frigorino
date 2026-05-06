using System.Text.Json;

namespace Frigorino.IntegrationTests.Infrastructure;

public class TestApiClient(ScenarioContextHolder ctx)
{
    private IReadOnlyDictionary<string, string> AuthHeaders => new Dictionary<string, string>
    {
        ["X-Test-User"] = ctx.UserContext.UserId,
        ["X-Test-Email"] = ctx.UserContext.Email,
        ["X-Test-Name"] = ctx.UserContext.Name,
    };

    private async Task<JsonElement> PostAsync(string url, object body)
    {
        var response = await ctx.BrowserContext.APIRequest.PostAsync(url, new APIRequestContextOptions
        {
            DataObject = body,
            Headers = AuthHeaders,
        });

        if (!response.Ok)
            throw new Exception($"API POST {url} failed: {response.Status} {response.StatusText}");

        return (await response.JsonAsync())!.Value;
    }

    private async Task PostVoidAsync(string url)
    {
        var response = await ctx.BrowserContext.APIRequest.PostAsync(url, new APIRequestContextOptions
        {
            Headers = AuthHeaders,
        });

        if (!response.Ok)
            throw new Exception($"API POST {url} failed: {response.Status} {response.StatusText}");
    }

    private async Task PutVoidAsync(string url, object body)
    {
        var response = await ctx.BrowserContext.APIRequest.PutAsync(url, new APIRequestContextOptions
        {
            DataObject = body,
            Headers = AuthHeaders,
        });

        if (!response.Ok)
            throw new Exception($"API PUT {url} failed: {response.Status} {response.StatusText}");
    }

    public async Task<int> CreateHouseholdAsync(string name)
    {
        var json = await PostAsync("/api/household", new { name });
        return json.GetProperty("id").GetInt32();
    }

    public async Task SetCurrentHouseholdAsync(int householdId)
    {
        await PutVoidAsync("/api/me/active-household", new { householdId });
    }

    public async Task<int> CreateListAsync(string name)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/lists", new { name });
        return json.GetProperty("id").GetInt32();
    }

    public async Task<int> CreateListItemAsync(int listId, string text)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/lists/{listId}/ListItems", new { text });
        return json.GetProperty("id").GetInt32();
    }

    public async Task<int> CreateInventoryAsync(string name)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/Inventories", new { name });
        return json.GetProperty("id").GetInt32();
    }

    public async Task<int> CreateInventoryItemAsync(int inventoryId, string text)
    {
        var json = await PostAsync($"/api/inventory/{inventoryId}/InventoryItems", new { text });
        return json.GetProperty("id").GetInt32();
    }
}
