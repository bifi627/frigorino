namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeSectionApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I GET the sections of recipe {string} via the API")]
    public async Task WhenIGetSections(string recipeName)
    {
        ctx.LastApiResponse = await api.TryGetRecipeSectionsAsync(ctx.RecipeIds[recipeName]);
    }

    [When("I GET the items of recipe {string} via the API")]
    public async Task WhenIGetItems(string recipeName)
    {
        ctx.LastApiResponse = await api.TryGetRecipeItemsAsync(ctx.RecipeIds[recipeName]);
    }

    [Then("the API sections of recipe {string} number {int}")]
    public async Task ThenSectionsNumber(string recipeName, int expected)
    {
        var response = await api.TryGetRecipeSectionsAsync(ctx.RecipeIds[recipeName]);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        Assert.Equal(expected, json.GetArrayLength());
    }

    // Used both as a When (the action under test) and as a Given/And setup step in other scenarios;
    // this project's Reqnroll matches step keyword-sensitively, so both attributes are required.
    [Given("I POST a section named {string} to recipe {string} via the API")]
    [When("I POST a section named {string} to recipe {string} via the API")]
    public async Task WhenIPostSection(string sectionName, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryCreateRecipeSectionAsync(recipeId, sectionName);
        ctx.LastApiResponse = response;
        if (response.Status == 201)
        {
            var json = (await response.JsonAsync())!.Value;
            ctx.RecipeSectionIds[(recipeName, sectionName)] = json.GetProperty("id").GetInt32();
        }
    }

    [Given("the recipe {string} has an item {string} in section {string}")]
    public async Task GivenItemInSection(string recipeName, string itemText, string sectionName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var sectionId = ctx.RecipeSectionIds[(recipeName, sectionName)];
        var itemId = await api.CreateRecipeItemInSectionAsync(recipeId, sectionId, itemText);
        ctx.SetRecipeItemId(recipeName, itemText, itemId);
    }

    [When("I DELETE the section {string} of recipe {string} via the API")]
    public async Task WhenIDeleteSection(string sectionName, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryDeleteRecipeSectionAsync(recipeId, ctx.RecipeSectionIds[(recipeName, sectionName)]);
    }

    [When("I DELETE the only section of recipe {string} via the API")]
    public async Task WhenIDeleteOnlySection(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var sectionId = await api.FirstSectionIdAsync(recipeId);
        ctx.LastApiResponse = await api.TryDeleteRecipeSectionAsync(recipeId, sectionId);
    }

    [When("I POST restore for the section {string} of recipe {string} via the API")]
    public async Task WhenIRestoreSection(string sectionName, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryRestoreRecipeSectionAsync(recipeId, ctx.RecipeSectionIds[(recipeName, sectionName)]);
    }

    [Then("the recipe item {string} is in section {string}")]
    public async Task ThenItemInSection(string itemText, string sectionName)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        var item = json.EnumerateArray().First(e => e.GetProperty("text").GetString() == itemText);
        var sectionId = item.GetProperty("sectionId").GetInt32();
        // The section name isn't on the item; assert it matches the captured id for that name.
        Assert.Equal(ctx.RecipeSectionIds.First(kv => kv.Key.Section == sectionName).Value, sectionId);
    }
}
