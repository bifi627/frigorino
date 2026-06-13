namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class BlueprintApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I create a blueprint named {string} ordered {string} via the API")]
    public async Task WhenICreateABlueprintNamedOrdered(string name, string orderedCategories)
    {
        var categories = orderedCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var blueprintId = await api.CreateBlueprintAsync(name, categories);
        ctx.BlueprintIds[name] = blueprintId;
    }

    [When("I apply blueprint {string} to {string} via the API")]
    public async Task WhenIApplyBlueprintTo(string blueprintName, string listName)
    {
        var blueprintId = ctx.BlueprintIds[blueprintName];
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryApplyBlueprintAsync(listId, blueprintId);
        Assert.Equal(200, ctx.LastApiResponse.Status);
    }

    [Then("the unchecked items of {string} are ordered {string}")]
    public async Task ThenTheUncheckedItemsAreOrdered(string listName, string expectedOrder)
    {
        var listId = ctx.ListIds[listName];
        var expected = expectedOrder
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToArray();

        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;

        var actual = json.EnumerateArray()
            .Where(i => !i.GetProperty("status").GetBoolean())
            .OrderBy(i => i.GetProperty("rank").GetString(), StringComparer.Ordinal)
            .Select(i => i.GetProperty("text").GetString()!.ToLowerInvariant())
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
