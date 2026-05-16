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

    public Task<IAPIResponse> TryCreateHouseholdAsync(string? name)
    {
        return ctx.BrowserContext.APIRequest.PostAsync("/api/household", new APIRequestContextOptions
        {
            DataObject = new { name },
            Headers = AuthHeaders,
        });
    }

    public Task<IAPIResponse> TrySetCurrentHouseholdAsync(int householdId)
    {
        return ctx.BrowserContext.APIRequest.PutAsync("/api/me/active-household", new APIRequestContextOptions
        {
            DataObject = new { householdId },
            Headers = AuthHeaders,
        });
    }

    public async Task<int> CreateListAsync(string name)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/lists", new { name });
        return json.GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryCreateListAsync(string? name, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists",
            new APIRequestContextOptions
            {
                DataObject = new { name },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryGetListsAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/lists",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryDeleteListAsync(int listId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public async Task<int> CreateListItemAsync(int listId, string text)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/lists/{listId}/items", new { text });
        return json.GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryGetListItemsAsync(int listId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCreateListItemAsync(int listId, string? text, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items",
            new APIRequestContextOptions
            {
                DataObject = new { text },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryDeleteListItemAsync(int listId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCompactListItemsAsync(int listId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/compact",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryReorderListItemAsync(int listId, int itemId, int afterId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PatchAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}/reorder",
            new APIRequestContextOptions
            {
                DataObject = new { afterId },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryToggleListItemStatusAsync(int listId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PatchAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}/toggle-status",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public async Task<int> CreateInventoryAsync(string name)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/inventories", new { name });
        return json.GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryCreateInventoryAsync(string? name, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/inventories",
            new APIRequestContextOptions
            {
                DataObject = new { name },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryGetInventoriesAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryDeleteInventoryAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public async Task<int> CreateInventoryItemAsync(int inventoryId, string text)
    {
        var json = await PostAsync(
            $"/api/household/{ctx.HouseholdId}/inventories/{inventoryId}/items",
            new { text });
        return json.GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryGetInventoryItemsAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCreateInventoryItemAsync(int inventoryId, string? text, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items",
            new APIRequestContextOptions
            {
                DataObject = new { text },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryDeleteInventoryItemAsync(int inventoryId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items/{itemId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCompactInventoryItemsAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items/compact",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryReorderInventoryItemAsync(int inventoryId, int itemId, int afterId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PatchAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items/{itemId}/reorder",
            new APIRequestContextOptions
            {
                DataObject = new { afterId },
                Headers = AuthHeaders,
            });
    }
}
