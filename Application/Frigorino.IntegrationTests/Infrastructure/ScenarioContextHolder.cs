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
    public Dictionary<string, int> ListItemIds { get; } = new();
    public Dictionary<string, int> InventoryIds { get; } = new();
    public IAPIResponse? LastApiResponse { get; set; }
}
