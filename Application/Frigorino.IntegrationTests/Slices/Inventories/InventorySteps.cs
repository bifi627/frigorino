namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class InventorySteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("there is an inventory named {string}")]
    public async Task GivenThereIsAnInventoryNamed(string name)
    {
        var inventoryId = await api.CreateInventoryAsync(name);
        ctx.InventoryIds[name] = inventoryId;
    }

    [When("I open the inventory {string}")]
    public async Task WhenIOpenTheInventory(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        await ctx.Page.GotoAsync($"/inventories/{inventoryId}/view", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    [When("I fill in the inventory name {string}")]
    public async Task WhenIFillInTheInventoryName(string name)
    {
        await ctx.Page.GetByRole(AriaRole.Textbox).First.FillAsync(name);
    }

    [When("I submit the inventory form")]
    public async Task WhenISubmitTheInventoryForm()
    {
        // Wait for the POST 201 BEFORE the URL wait so a server-side failure surfaces as a
        // precise response-match miss instead of an opaque "URL didn't change in 30s". Form's
        // catch swallows mutation errors to console only, so without this the test has no
        // visibility into the actual cause when CI contention slows the POST past 30s.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/inventories")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("inventory-create-submit-button").ClickAsync();
        await responseTask;
        await ctx.Page.WaitForURLAsync("**/inventories/*/view");
    }

    [When("I add item {string} to the inventory")]
    public async Task WhenIAddItemToTheInventory(string itemText)
    {
        await ctx.Page.GetByTestId("autocomplete-input-textfield").ClickAsync();
        await ctx.Page.GetByTestId("autocomplete-input-textfield").PressSequentiallyAsync(itemText);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
    }

    [When("I delete the inventory {string}")]
    public async Task WhenIDeleteTheInventory(string inventoryName)
    {
        await ctx.Page.GetByTestId($"inventory-item-menu-button-{inventoryName}").ClickAsync();
        await ctx.Page.GetByTestId("delete-inventory-button").ClickAsync();
    }

    [When("I open the inventory edit page for {string}")]
    public async Task WhenIOpenTheInventoryEditPageFor(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        await ctx.Page.GotoAsync($"/inventories/{inventoryId}/edit", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    [When("I save the inventory")]
    public async Task WhenISaveTheInventory()
    {
        // Wait for the PUT before the next step inspects the post-save DOM/route — same pattern
        // as ListSteps.WhenISaveTheList.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/inventories/")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId("inventory-edit-save-button").ClickAsync();
        await responseTask;
    }

    [Then("I am on the inventory view page")]
    public async Task ThenIAmOnTheInventoryViewPage()
    {
        await ctx.Page.WaitForURLAsync("**/inventories/*/view");
    }

    [Then("{string} appears in the inventory overview")]
    public async Task ThenAppearsInTheInventoryOverview(string inventoryName)
    {
        var item = ctx.Page.GetByTestId($"inventory-item-{inventoryName}");
        await Assertions.Expect(item).ToBeVisibleAsync();
    }

    [Then("{string} appears in the inventory")]
    public async Task ThenAppearsInTheInventory(string itemText)
    {
        await ctx.Page.GetByText(itemText).First.WaitForAsync();
    }

    [Then("{string} no longer appears in the inventory overview")]
    public async Task ThenNoLongerAppearsInTheInventoryOverview(string inventoryName)
    {
        var item = ctx.Page.GetByTestId($"inventory-item-{inventoryName}");
        await Assertions.Expect(item).Not.ToBeVisibleAsync();
    }
}
