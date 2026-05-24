namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class ComposerSteps(ScenarioContextHolder ctx)
{
    [When("I type {string} in the composer")]
    public async Task WhenITypeInTheComposer(string text)
    {
        await ctx.Page.GetByTestId("autocomplete-input-textfield").ClickAsync();
        await ctx.Page.GetByTestId("autocomplete-input-textfield").PressSequentiallyAsync(text);
    }

    [When("I open the {string} composer panel")]
    public async Task WhenIOpenTheComposerPanel(string featureId)
    {
        await ctx.Page.GetByTestId($"composer-toggle-{featureId}").ClickAsync();
        // The panel content stays mounted inside a collapsed MUI Collapse, so wait for
        // VISIBLE (not merely attached) before the follow-up step fills its input.
        await Assertions.Expect(ctx.Page.GetByTestId($"composer-panel-{featureId}"))
            .ToBeVisibleAsync();
    }

    [When("I set the quantity to {string}")]
    public async Task WhenISetTheQuantityTo(string quantity)
    {
        await ctx.Page.Locator("[data-testid='composer-panel-quantity'] input")
            .FillAsync(quantity);
    }

    [When("I set the expiry date to {string}")]
    public async Task WhenISetTheExpiryDateTo(string isoDate)
    {
        await ctx.Page.Locator("[data-testid='composer-panel-expiry'] input")
            .FillAsync(isoDate);
    }

    [When("I start editing the item")]
    public async Task WhenIStartEditingTheItem()
    {
        await ctx.Page.GetByTestId("edit-item-button").ClickAsync();
    }

    [When("I submit the composer")]
    public async Task WhenISubmitTheComposer()
    {
        // Await the POST so the follow-up Then-step reads post-server-confirm DOM rather than
        // the optimistic-update window. Matches the wait in WhenIAddItemToTheList.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/items")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        await responseTask;
    }

    [When("I save the composer edit")]
    public async Task WhenISaveTheComposerEdit()
    {
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        await responseTask;
    }

    [Then("the {string} composer chip is visible")]
    public async Task ThenTheComposerChipIsVisible(string featureId)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"composer-chip-{featureId}"))
            .ToBeVisibleAsync();
    }
}
