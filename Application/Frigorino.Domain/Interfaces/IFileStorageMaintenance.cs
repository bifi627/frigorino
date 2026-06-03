namespace Frigorino.Domain.Interfaces
{
    // Listing capability for blob maintenance (orphan reclamation), kept separate from the lean
    // hot-path IFileStorage so upload/serve slices don't depend on enumeration. Each backend
    // enumerates only its OWN namespace, so a sweep can never see foreign objects in a shared bucket.
    public interface IFileStorageMaintenance
    {
        IAsyncEnumerable<StoredBlob> ListAsync(CancellationToken ct);
    }

    // One stored blob: its opaque key (bare, exactly as stored in the DB) and storage-side creation
    // time (used by the sweep's grace-period filter).
    public readonly record struct StoredBlob(string Key, DateTimeOffset CreatedAt);
}
