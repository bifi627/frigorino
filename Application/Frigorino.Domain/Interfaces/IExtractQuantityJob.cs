namespace Frigorino.Domain.Interfaces
{
    // Unit of work enqueued onto the background runner; resolved in a fresh DI scope.
    public interface IExtractQuantityJob
    {
        Task Run(int householdId, int listId, int itemId, string rawText, CancellationToken ct);
    }
}
