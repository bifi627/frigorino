using System.Text.Json;

namespace Frigorino.IntegrationTests.Slices.Settings;

[Binding]
public class SettingsApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // ---- User settings ----

    [When("I GET my user settings via the API")]
    public async Task WhenIGetMyUserSettingsViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetUserSettingsAsync();
    }

    [When("I PUT my user settings language {string} via the API")]
    public async Task WhenIPutMyUserSettingsLanguageViaTheApi(string language)
    {
        ctx.LastApiResponse = await api.TryUpdateUserSettingsAsync(language);
    }

    [When("I PUT my user settings language to null via the API")]
    public async Task WhenIPutMyUserSettingsLanguageToNullViaTheApi()
    {
        ctx.LastApiResponse = await api.TryUpdateUserSettingsAsync(null);
    }

    // ---- Household settings ----

    [When("I GET the household settings via the API")]
    public async Task WhenIGetTheHouseholdSettingsViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetHouseholdSettingsAsync();
    }

    [When("I PUT the household settings retention {int} via the API")]
    public async Task WhenIPutTheHouseholdSettingsRetentionViaTheApi(int retentionDays)
    {
        ctx.LastApiResponse = await api.TryUpdateHouseholdSettingsAsync(retentionDays);
    }

    // ---- Inventory settings ----

    [When("I GET the settings of inventory {string} via the API")]
    public async Task WhenIGetTheSettingsOfInventoryViaTheApi(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryGetInventorySettingsAsync(inventoryId);
    }

    [When("I PUT the settings of inventory {string} lead {int} via the API")]
    public async Task WhenIPutTheSettingsOfInventoryLeadViaTheApi(string inventoryName, int lead)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryUpdateInventorySettingsAsync(inventoryId, lead);
    }

    [When("I PUT the settings of inventory {string} lead to null via the API")]
    public async Task WhenIPutTheSettingsOfInventoryLeadToNullViaTheApi(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryUpdateInventorySettingsAsync(inventoryId, null);
    }

    // ---- Response body assertions ----

    [Then("the API response language is {string}")]
    public async Task ThenTheApiResponseLanguageIs(string expected)
    {
        var body = await ReadBodyAsync();
        var language = body.GetProperty("language");
        Assert.Equal(JsonValueKind.String, language.ValueKind);
        Assert.Equal(expected, language.GetString());
    }

    [Then("the API response has no language")]
    public async Task ThenTheApiResponseHasNoLanguage()
    {
        var body = await ReadBodyAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("language").ValueKind);
    }

    [Then("the API response retention is {int}")]
    public async Task ThenTheApiResponseRetentionIs(int expected)
    {
        var body = await ReadBodyAsync();
        Assert.Equal(expected, body.GetProperty("checkedItemRetentionDays").GetInt32());
    }

    [Then("the API response lead is {int}")]
    public async Task ThenTheApiResponseLeadIs(int expected)
    {
        var body = await ReadBodyAsync();
        var lead = body.GetProperty("expiryLeadDays");
        Assert.Equal(JsonValueKind.Number, lead.ValueKind);
        Assert.Equal(expected, lead.GetInt32());
    }

    [Then("the API response has no lead")]
    public async Task ThenTheApiResponseHasNoLead()
    {
        var body = await ReadBodyAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("expiryLeadDays").ValueKind);
    }

    private async Task<JsonElement> ReadBodyAsync()
    {
        Assert.NotNull(ctx.LastApiResponse);
        var body = await ctx.LastApiResponse.JsonAsync();
        Assert.NotNull(body);
        return body.Value;
    }
}
