namespace Frigorino.IntegrationTests.Infrastructure;

public class ScenarioContextHolder
{
    public TestWebApplicationFactory Factory { get; set; } = null!;
    public IBrowserContext BrowserContext { get; set; } = null!;
    public IPage Page { get; set; } = null!;
    public TestUserContext UserContext { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public int HouseholdId { get; set; }
    public Dictionary<string, int> ListIds { get; } = new();
    public Dictionary<string, int> InventoryIds { get; } = new();
    public Dictionary<string, int> BlueprintIds { get; } = new();
    public Dictionary<string, int> RecipeIds { get; } = new();
    public Dictionary<(string Recipe, string Section), int> RecipeSectionIds { get; } = new();
    public Dictionary<(string Recipe, string Label), int> RecipeLinkIds { get; } = new();
    public IAPIResponse? LastApiResponse { get; set; }
    public IAPIResponse[]? ConcurrentApiResponses { get; set; }

    private readonly Dictionary<(string list, string text), int> _listItemIds = new();
    private readonly Dictionary<(string inventory, string text), int> _inventoryItemIds = new();
    private readonly Dictionary<(string recipe, string text), int> _recipeItemIds = new();

    public void SetListItemId(string listName, string itemText, int id)
    {
        _listItemIds[(listName, itemText)] = id;
    }

    public int GetListItemId(string listName, string itemText)
    {
        return _listItemIds[(listName, itemText)];
    }

    public void SetInventoryItemId(string inventoryName, string itemText, int id)
    {
        _inventoryItemIds[(inventoryName, itemText)] = id;
    }

    public int GetInventoryItemId(string inventoryName, string itemText)
    {
        return _inventoryItemIds[(inventoryName, itemText)];
    }

    public void SetRecipeItemId(string recipeName, string itemText, int id)
    {
        _recipeItemIds[(recipeName, itemText)] = id;
    }

    public int GetRecipeItemId(string recipeName, string itemText)
    {
        return _recipeItemIds[(recipeName, itemText)];
    }
}
