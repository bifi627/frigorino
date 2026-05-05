using Frigorino.Domain.Entities;
using Org.BouncyCastle.Crypto.Engines;
using Xunit;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        await ctx.Page.GetByTestId("inventory-create-submit-button").ClickAsync();
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
        //await ctx.Page.WaitForResponseAsync("**/api/**/inventories/**");
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
