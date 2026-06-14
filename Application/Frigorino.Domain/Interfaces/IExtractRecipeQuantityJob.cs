namespace Frigorino.Domain.Interfaces
{
    public interface IExtractRecipeQuantityJob
    {
        Task Run(int householdId, int recipeId, int itemId, string rawText, CancellationToken ct);
    }
}
