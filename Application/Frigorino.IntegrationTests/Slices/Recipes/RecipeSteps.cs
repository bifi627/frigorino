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

    [Given("there is a recipe named {string} with servings {int}")]
    public async Task GivenThereIsARecipeNamedWithServings(string name, int servings)
    {
        var recipeId = await api.CreateRecipeWithServingsAsync(name, servings);
        ctx.RecipeIds[name] = recipeId;
    }

    [Given("the recipe {string} has an ingredient {string} with quantity {double} {string}")]
    public async Task GivenTheRecipeHasIngredientWithQuantity(
        string recipeName, string itemText, double value, string unit)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = await api.CreateRecipeItemAsync(recipeId, itemText);
        await api.TrySetRecipeItemQuantityAsync(recipeId, itemId, value, unit);
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

    [When("I open the recipe {string} for editing")]
    public async Task WhenIOpenTheRecipeForEditing(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        await ctx.Page.GotoAsync(
            $"/recipes/{recipeId}/edit",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    // ---- View interactions ----

    [When("I increment the servings")]
    public async Task WhenIIncrementTheServings()
    {
        // Display-only scaling: stepping servings up multiplies each ingredient's shown
        // quantity (effectiveServings / baseServings) — no server round-trip.
        await ctx.Page.GetByTestId("recipe-servings-increment").ClickAsync();
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
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
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

    [When("I confirm deleting the recipe {string} from the card menu")]
    public async Task WhenIConfirmDeletingTheRecipeFromTheCardMenu(string recipeName)
    {
        // The card-menu delete opens a type-the-name confirmation dialog (guards against an
        // accidental tap permanently deleting a recipe). Type the exact name to enable confirm.
        await ctx.Page.GetByTestId("delete-recipe-button").ClickAsync();
        await ctx.Page.GetByTestId("recipe-delete-confirm-input").FillAsync(recipeName);

        // Await the DELETE 204 so the next Then-step inspects post-server-confirm DOM.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/recipes/")
            && r.Request.Method == "DELETE"
            && r.Status == 204);
        await ctx.Page.GetByTestId("recipe-delete-confirm-button").ClickAsync();
        await responseTask;
    }

    // ---- Edit actions ----

    [When("I tap the edit recipe button")]
    public async Task WhenITapTheEditRecipeButton()
    {
        await ctx.Page.GetByTestId("recipe-edit-button").ClickAsync();
    }

    [When("I set the recipe description to {string}")]
    public async Task WhenISetTheRecipeDescriptionTo(string text)
    {
        // Fill the description, then blur to flush the debounced auto-save, and await the
        // recipe (not item) PUT so the follow-up navigation reads post-save state.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/recipes/")
            && !r.Url.Contains("/items")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        var description = ctx.Page.GetByTestId("recipe-description-input");
        await description.FillAsync(text);
        await description.BlurAsync();
        await responseTask;
    }

    // ---- Collapsible sections (edit page) ----

    [When("I collapse the {string} recipe section")]
    public async Task WhenICollapseTheRecipeSection(string section)
    {
        // The Accordion summary toggles on click; only click if currently expanded so the
        // step is deterministic regardless of the (persisted) starting state.
        var summary = ctx.Page.GetByTestId($"recipe-section-{section}-summary");
        if (await summary.GetAttributeAsync("aria-expanded") == "true")
        {
            await summary.ClickAsync();
        }
        await Assertions.Expect(summary).ToHaveAttributeAsync("aria-expanded", "false");
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

    [Then("I am on the recipe edit page for {string}")]
    public async Task ThenIAmOnTheRecipeEditPageFor(string recipeName)
    {
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-items")).ToBeVisibleAsync();
        // The composer (add-ingredient) is present on the edit page.
        await Assertions.Expect(
            ctx.Page.GetByTestId("autocomplete-input-textfield")).ToBeVisibleAsync();
    }

    [Then("the recipe view is read-only")]
    public async Task ThenTheRecipeViewIsReadOnly()
    {
        // No add-ingredient composer on the read-only view.
        await Assertions.Expect(
            ctx.Page.GetByTestId("autocomplete-input-textfield")).Not.ToBeVisibleAsync();
        // But the edit affordance is present.
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-edit-button")).ToBeVisibleAsync();
    }

    [Then("the recipe description shows {string}")]
    public async Task ThenTheRecipeDescriptionShows(string text)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-description"))
            .ToContainTextAsync(text);
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

    [Then("the {string} recipe section is collapsed")]
    public async Task ThenTheRecipeSectionIsCollapsed(string section)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-section-{section}-summary"))
            .ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Then("the recipe edit composer is visible")]
    public async Task ThenTheRecipeEditComposerIsVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-composer-footer"))
            .ToBeVisibleAsync();
    }

    [Then("the recipe edit composer is hidden")]
    public async Task ThenTheRecipeEditComposerIsHidden()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-composer-footer"))
            .Not.ToBeVisibleAsync();
    }
}
