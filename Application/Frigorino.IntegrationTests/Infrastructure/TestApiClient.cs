using System.Text;
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

    // ---- Sort blueprints ----

    public async Task<int> CreateBlueprintAsync(string name, IEnumerable<string> categories)
    {
        var json = await PostAsync(
            $"/api/household/{ctx.HouseholdId}/blueprints",
            new { name, categories });
        return json.GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryApplyBlueprintAsync(int listId, int blueprintId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/apply-blueprint",
            new APIRequestContextOptions
            {
                DataObject = new { blueprintId },
                Headers = AuthHeaders,
            });
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

    public Task<IAPIResponse> TryGetListRevisionAsync(int listId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/revision",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetInventoryRevisionAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/revision",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetExpiryCalendarRevisionAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/calendar/revision",
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

    // ---- Recipes ----

    public async Task<int> CreateRecipeAsync(string name)
    {
        var json = await PostAsync($"/api/household/{ctx.HouseholdId}/recipes", new { name, description = (string?)null });
        return json.GetProperty("id").GetInt32();
    }

    // Resolves the recipe's first section, then creates the item in it. Keeps existing
    // recipe-item scenarios working now that item-create requires a sectionId.
    public async Task<int> CreateRecipeItemAsync(int recipeId, string text)
        => await CreateRecipeItemInSectionAsync(recipeId, await FirstSectionIdAsync(recipeId), text);

    public async Task<int> CreateRecipeItemInSectionAsync(int recipeId, int sectionId, string text)
    {
        var json = await PostAsync(
            $"/api/household/{ctx.HouseholdId}/recipes/{recipeId}/items",
            new { sectionId, text, comment = (string?)null });
        return json.GetProperty("id").GetInt32();
    }

    public async Task<int> FirstSectionIdAsync(int recipeId)
    {
        var resp = await TryGetRecipeSectionsAsync(recipeId);
        var json = (await resp.JsonAsync())!.Value;
        return json.EnumerateArray().First().GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryGetRecipeSectionsAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/sections",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCreateRecipeSectionAsync(int recipeId, string? name, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/sections",
            new APIRequestContextOptions
            {
                DataObject = new { name, description = (string?)null },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryDeleteRecipeSectionAsync(int recipeId, int sectionId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/sections/{sectionId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryRestoreRecipeSectionAsync(int recipeId, int sectionId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/sections/{sectionId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetRecipeLinksAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCreateRecipeLinkAsync(int recipeId, string? url, string? label = null, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links",
            new APIRequestContextOptions
            {
                DataObject = new { url, label },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryDeleteRecipeLinkAsync(int recipeId, int linkId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links/{linkId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryRestoreRecipeLinkAsync(int recipeId, int linkId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links/{linkId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCreateRecipeItemInSectionAsync(int recipeId, int sectionId, string? text, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/items",
            new APIRequestContextOptions
            {
                DataObject = new { sectionId, text, comment = (string?)null },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryCreateRecipeAsync(string? name, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes",
            new APIRequestContextOptions
            {
                DataObject = new { name, description = (string?)null },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryGetRecipesAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryDeleteRecipeAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetRecipeRevisionAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/revision",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetRecipeItemsAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/items",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public async Task<IAPIResponse> TryCreateRecipeItemAsync(int recipeId, string? text, int? householdId = null)
        => await TryCreateRecipeItemInSectionAsync(recipeId, await FirstSectionIdAsync(recipeId), text, householdId);

    public Task<IAPIResponse> TryDeleteRecipeItemAsync(int recipeId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/items/{itemId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryRestoreRecipeItemAsync(int recipeId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/items/{itemId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryReorderRecipeItemAsync(int recipeId, int itemId, int afterId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PatchAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/items/{itemId}/reorder",
            new APIRequestContextOptions
            {
                DataObject = new { afterId },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryCreateRecipeWithServingsAsync(string name, int? servings, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes",
            new APIRequestContextOptions
            {
                DataObject = new { name, description = (string?)null, servings },
                Headers = AuthHeaders,
            });
    }

    public async Task<int> CreateRecipeWithServingsAsync(string name, int servings)
    {
        var response = await TryCreateRecipeWithServingsAsync(name, servings);
        if (!response.Ok)
        {
            throw new Exception(
                $"CreateRecipeWithServingsAsync failed: {response.Status} {await response.TextAsync()}");
        }

        var json = await response.JsonAsync();
        return json!.Value.GetProperty("id").GetInt32();
    }

    // Sets a numeric quantity on a recipe item via the item update endpoint, so scaling
    // scenarios have a deterministic quantity to scale (extraction is async/non-deterministic).
    public Task<IAPIResponse> TrySetRecipeItemQuantityAsync(
        int recipeId, int itemId, double value, string unit, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/items/{itemId}",
            new APIRequestContextOptions
            {
                DataObject = new { quantity = new { value, unit }, clearQuantity = false },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryUpdateRecipeAsync(int recipeId, string name, int? servings, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}",
            new APIRequestContextOptions
            {
                DataObject = new { name, description = (string?)null, servings },
                Headers = AuthHeaders,
            });
    }

    // Seeds a recipe item that already carries a structured quantity, deterministically: create the
    // item, then PUT the quantity (the create endpoint routes plain text through async extraction, so
    // a direct quantity write is the only deterministic path). unit is the QuantityUnit string name.
    public async Task<int> CreateRecipeItemWithQuantityAsync(int recipeId, string text, decimal value, string unit)
    {
        var itemId = await CreateRecipeItemAsync(recipeId, text);
        var response = await TrySetRecipeItemQuantityAsync(recipeId, itemId, (double)value, unit);
        if (!response.Ok)
        {
            throw new Exception(
                $"CreateRecipeItemWithQuantityAsync failed to set quantity on item {itemId}: {response.Status} {await response.TextAsync()}");
        }
        return itemId;
    }

    // Calls the copy-to-list endpoint. items = sequence of anonymous { recipeItemId, quantity } objects
    // where quantity is a { value, unit } object or null (text-only).
    public Task<IAPIResponse> TryCopyRecipeToListAsync(
        int recipeId, int targetListId, IEnumerable<object> items, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/copy-to-list",
            new APIRequestContextOptions
            {
                DataObject = new { targetListId, items },
                Headers = AuthHeaders,
            });
    }

    // ---- Recipe attachments (multipart upload + byte-serving) ----

    // Minimal PDF bytes — the document path stores the raw bytes as-is (no parsing), so a header +
    // EOF marker is enough to round-trip and serve back with content-type application/pdf.
    private static readonly byte[] TinyPdf = Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

    public Task<IAPIResponse> TryUploadRecipeImageAttachmentAsync(int recipeId, string caption = "", int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        var form = ctx.BrowserContext.APIRequest.CreateFormData();
        form.Append("file", new FilePayload { Name = "photo.png", MimeType = "image/png", Buffer = TinyPng });
        form.Append("caption", caption);
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/attachments",
            new APIRequestContextOptions { Headers = AuthHeaders, Multipart = form });
    }

    public Task<IAPIResponse> TryUploadRecipeDocumentAttachmentAsync(int recipeId, string caption = "", int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        var form = ctx.BrowserContext.APIRequest.CreateFormData();
        form.Append("file", new FilePayload { Name = "sheet.pdf", MimeType = "application/pdf", Buffer = TinyPdf });
        form.Append("caption", caption);
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/attachments",
            new APIRequestContextOptions { Headers = AuthHeaders, Multipart = form });
    }

    public async Task<int> CreateRecipeImageAttachmentAsync(int recipeId, string caption = "")
    {
        var response = await TryUploadRecipeImageAttachmentAsync(recipeId, caption);
        if (!response.Ok)
        {
            throw new Exception($"CreateRecipeImageAttachmentAsync failed: {response.Status} {await response.TextAsync()}");
        }

        var json = await response.JsonAsync();
        return json!.Value.GetProperty("id").GetInt32();
    }

    public async Task<int> CreateRecipeDocumentAttachmentAsync(int recipeId, string caption = "")
    {
        var response = await TryUploadRecipeDocumentAttachmentAsync(recipeId, caption);
        if (!response.Ok)
        {
            throw new Exception($"CreateRecipeDocumentAttachmentAsync failed: {response.Status} {await response.TextAsync()}");
        }

        var json = await response.JsonAsync();
        return json!.Value.GetProperty("id").GetInt32();
    }

    public Task<IAPIResponse> TryGetRecipeAttachmentsAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/attachments",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetRecipeAttachmentFileAsync(int recipeId, int attachmentId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/attachments/{attachmentId}/file",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetRecipeAttachmentThumbnailAsync(int recipeId, int attachmentId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/attachments/{attachmentId}/thumbnail",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
}
