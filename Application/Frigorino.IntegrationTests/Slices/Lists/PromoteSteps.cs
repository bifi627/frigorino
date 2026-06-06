namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class PromoteSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Then("the promote bar shows {int} item")]
    [Then("the promote bar shows {int} items")]
    public async Task ThenThePromoteBarShows(int count)
    {
        var bar = ctx.Page.GetByTestId("promote-bar");
        await Assertions.Expect(bar).ToBeVisibleAsync();
        await Assertions.Expect(bar).ToHaveAttributeAsync("data-count", count.ToString());
    }

    [When("I open the promote review sheet")]
    public async Task WhenIOpenThePromoteReviewSheet()
    {
        await ctx.Page.GetByTestId("promote-bar-review").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("promote-sheet")).ToBeVisibleAsync();
    }

    [When("I add the selected promote items")]
    public async Task WhenIAddTheSelectedPromoteItems()
    {
        // Subscribe before click — handleAdd fires a single atomic POST .../lists/{id}/promote
        // for the whole selected batch. EndsWith("/promote") excludes the "/promote/skip" route
        // used by omit / clear-all.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/promote")
            && r.Request.Method == "POST"
            && r.Status == 200);
        await ctx.Page.GetByTestId("promote-add-button").ClickAsync();
        await responseTask;
    }

    [When("I omit {string} from the promote sheet")]
    public async Task WhenIOmitFromThePromoteSheet(string itemText)
    {
        await ctx.Page.GetByTestId($"promote-row-omit-{itemText}").ClickAsync();
    }

    [Then("the inventory {string} contains an item {string}")]
    public async Task ThenTheInventoryContainsAnItem(string inventoryName, string itemText)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryItemsAsync(inventoryId);
        Assert.Equal(200, response.Status);
        var items = (await response.JsonAsync())!.Value;
        var texts = items.EnumerateArray()
            .Select(i => i.GetProperty("text").GetString())
            .ToList();
        Assert.Contains(itemText, texts);
    }

    [When("I deselect {string} in the promote sheet")]
    public async Task WhenIDeselectInThePromoteSheet(string itemText)
    {
        // The row checkbox is selected by default; clicking it deselects the row.
        await ctx.Page.GetByTestId($"promote-row-select-{itemText}").ClickAsync();
    }

    [Then("the promote add button is disabled")]
    public async Task ThenThePromoteAddButtonIsDisabled()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("promote-add-button"))
            .ToBeDisabledAsync();
    }

    [Then("the promote bar is not visible")]
    public async Task ThenThePromoteBarIsNotVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("promote-bar"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the promote sheet is not visible")]
    public async Task ThenThePromoteSheetIsNotVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("promote-sheet"))
            .Not.ToBeVisibleAsync();
    }

    [When("I clear the expiry date for {string}")]
    public async Task WhenIClearTheExpiryDateFor(string itemText)
    {
        // Masked DatePicker field: select-all + Delete clears every section. FillAsync("")
        // does not work on a segmented field.
        var input = ctx.Page.GetByTestId($"promote-row-expiry-{itemText}");
        await input.ClickAsync();
        await input.PressAsync("ControlOrMeta+a");
        await input.PressAsync("Delete");
    }
}
