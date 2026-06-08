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

    public Task<IAPIResponse> TryCreateListItemAsync(int listId, string? text, int? householdId = null, string? comment = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items",
            new APIRequestContextOptions
            {
                DataObject = new { text, comment },
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

    public Task<IAPIResponse> TryRestoreListItemAsync(int listId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }


    public Task<IAPIResponse> TryUpdateListItemAsync(int listId, int itemId, string? text, string? quantity, bool? status, int? householdId = null, string? comment = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}",
            new APIRequestContextOptions
            {
                DataObject = new { text, quantity, status, comment },
                Headers = AuthHeaders,
            });
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

    // ---- Media items (multipart upload + byte-serving) ----

    // Small 8x8 RGBA PNG (valid, CRC-correct — the decoder (Magick.NET) validates IDAT CRC) for upload scenarios.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAAFklEQVR4nGOpCDjxnwEPYGEgAIaHAgCvwgKw2JOr9gAAAABJRU5ErkJggg==");

    public Task<IAPIResponse> TryUploadImageAsync(int listId, string caption = "", int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        var form = ctx.BrowserContext.APIRequest.CreateFormData();
        form.Append("file", new FilePayload { Name = "photo.png", MimeType = "image/png", Buffer = TinyPng });
        form.Append("type", "Image");
        form.Append("caption", caption);
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/media",
            new APIRequestContextOptions { Headers = AuthHeaders, Multipart = form });
    }

    public Task<IAPIResponse> TryGetItemThumbnailAsync(int listId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}/thumbnail",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetItemFileAsync(int listId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}/file",
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

    public Task<IAPIResponse> TryGetExpiryCalendarAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/calendar",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    // Quantity is a structured QuantityDto on the wire ({ value, unit }). The seed helper takes a
    // plain number string (e.g. "5") and sends it as that many Pieces; null = no quantity.
    public async Task<int> CreateInventoryItemAsync(int inventoryId, string text, string? quantity = null)
    {
        object? quantityDto = string.IsNullOrWhiteSpace(quantity)
            ? null
            : new
            {
                value = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture),
                unit = "Piece",
            };
        var json = await PostAsync(
            $"/api/household/{ctx.HouseholdId}/inventories/{inventoryId}/items",
            new { text, quantity = quantityDto });
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

    public Task<IAPIResponse> TryRestoreInventoryItemAsync(int inventoryId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items/{itemId}/restore",
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

    // ---- Settings ----

    public Task<IAPIResponse> TryGetUserSettingsAsync()
    {
        return ctx.BrowserContext.APIRequest.GetAsync(
            "/api/me/settings",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryUpdateUserSettingsAsync(string? language)
    {
        return ctx.BrowserContext.APIRequest.PutAsync(
            "/api/me/settings",
            new APIRequestContextOptions
            {
                DataObject = new { language },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryGetHouseholdSettingsAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/settings",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryUpdateHouseholdSettingsAsync(int retentionDays, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/settings",
            new APIRequestContextOptions
            {
                DataObject = new { checkedItemRetentionDays = retentionDays },
                Headers = AuthHeaders,
            });
    }

    // ---- Notifications / maintenance ----

    // The machine-to-machine /internal/expiry-scan endpoint is key-guarded (X-Maintenance-Key) and
    // anonymous, so no test-user auth headers are sent — only the maintenance key.
    public Task<IAPIResponse> TryTriggerExpiryScanAsync(string maintenanceKey)
    {
        return ctx.BrowserContext.APIRequest.PostAsync(
            "/internal/expiry-scan",
            new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["X-Maintenance-Key"] = maintenanceKey },
            });
    }

    public Task<IAPIResponse> TryGetInventorySettingsAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/settings",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    // ---- Per-user inventory notification preferences ----

    public Task<IAPIResponse> TryGetMyInventoryNotificationAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/notifications",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryUpdateMyInventoryNotificationAsync(int inventoryId, bool enabled, int? leadDays, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/notifications",
            new APIRequestContextOptions
            {
                DataObject = new { enabled, leadDays },
                Headers = AuthHeaders,
            });
    }
}
