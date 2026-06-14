namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // ---- Seed helpers ----

    [Given("there is a recipe named {string}")]
    public async Task GivenThereIsARecipeNamed(string name)
    {
        var recipeId = await api.CreateRecipeAsync(name);
        ctx.RecipeIds[name] = recipeId;
    }

    // ---- Navigation ----

    [When("I open the recipe {string}")]
    public async Task WhenIOpenTheRecipe(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        await ctx.Page.GotoAsync(
            $"/recipes/{recipeId}/view",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    // ---- Create recipe form ----

    [When("I fill in the recipe name {string}")]
    public async Task WhenIFillInTheRecipeName(string name)
    {
        await ctx.Page.GetByRole(AriaRole.Textbox).First.FillAsync(name);
    }

    [When("I submit the recipe form")]
    public async Task WhenISubmitTheRecipeForm()
    {
        // Wait for the POST 201 before the URL assertion so a server-side failure
        // surfaces as a precise response miss rather than an opaque URL timeout.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/recipes")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("recipe-create-submit-button").ClickAsync();
        await responseTask;
        await ctx.Page.WaitForURLAsync("**/recipes/*/view");
    }

    // ---- Add ingredient via composer ----

    [When("I add ingredient {string} to the recipe")]
    public async Task WhenIAddIngredientToTheRecipe(string itemText)
    {
        // Await the POST so back-to-back "add ingredient" steps don't fire overlapping
        // requests and the DOM is stable for the follow-up assertion.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("autocomplete-input-textfield").ClickAsync();
        await ctx.Page.GetByTestId("autocomplete-input-textfield").PressSequentiallyAsync(itemText);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        await responseTask;
    }

    // ---- Ingredient item menu ----

    [When("I open the ingredient item menu for {string}")]
    public async Task WhenIOpenTheIngredientItemMenuFor(string itemText)
    {
        await ctx.Page.GetByTestId($"item-menu-button-{itemText}").ClickAsync();
    }

    // Save a recipe item edit and wait for the PUT to the recipe items endpoint.
    // Waits for any PUT to .../recipes/.../items/... (regardless of status) so we
    // can assert the exact status in the Then step rather than silently timeout.
    [When("I save the recipe item edit")]
    public async Task WhenISaveTheRecipeItemEdit()
    {
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/recipes/")
            && r.Url.Contains("/items/")
            && r.Request.Method == "PUT");
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        var response = await responseTask;
        if (response.Status != 200)
        {
            var body = await response.TextAsync();
            throw new Exception($"Recipe item PUT returned {response.Status}: {body}");
        }
    }

    // ---- Recipe card menu ----

    [When("I open the recipe card menu for {string}")]
    public async Task WhenIOpenTheRecipeCardMenuFor(string recipeName)
    {
        await ctx.Page.GetByTestId($"recipe-item-menu-button-{recipeName}").ClickAsync();
    }

    [When("I delete the recipe from the card menu")]
    public async Task WhenIDeleteTheRecipeFromTheCardMenu()
    {
        // Await the DELETE 204 so the next Then-step inspects post-server-confirm DOM.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/recipes/")
            && r.Request.Method == "DELETE"
            && r.Status == 204);
        await ctx.Page.GetByTestId("delete-recipe-button").ClickAsync();
        await responseTask;
    }

    // ---- Assertions ----

    [Then("I am on the recipe view page for {string}")]
    public async Task ThenIAmOnTheRecipeViewPageFor(string recipeName)
    {
        await ctx.Page.WaitForURLAsync("**/recipes/*/view");
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-items")).ToBeVisibleAsync();
        // Verify the recipe name is visible in the page header (PageHeadActionBar title).
        await ctx.Page.GetByText(recipeName).First.WaitForAsync();
    }

    [Then("{string} appears in the recipe items")]
    public async Task ThenAppearsInTheRecipeItems(string itemText)
    {
        // The recipe items container is recipe-items; item rows render the text inside
        // RecipeItemContent. Assert by text within the container.
        await ctx.Page.GetByTestId("recipe-items")
            .GetByText(itemText).First.WaitForAsync();
    }

    [Then("the recipe item {string} shows quantity {string}")]
    public async Task ThenTheRecipeItemShowsQuantity(string itemText, string quantity)
    {
        // RecipeItemContent renders ItemQuantityChip with testId=recipe-item-quantity-{item.text}.
        // The chip may also render a unit label next to the value — assert containment.
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-item-quantity-{itemText}"))
            .ToContainTextAsync(quantity);
    }

    [Then("{string} no longer appears in the recipe overview")]
    public async Task ThenNoLongerAppearsInTheRecipeOverview(string recipeName)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-item-{recipeName}"))
            .Not.ToBeVisibleAsync();
    }
}
