namespace Frigorino.Domain.Interfaces
{
    // The unit of work enqueued onto the background runner. Resolved in a fresh DI scope.
    public interface IClassifyProductJob
    {
        Task Run(int householdId, string rawName, CancellationToken ct);
    }
}
