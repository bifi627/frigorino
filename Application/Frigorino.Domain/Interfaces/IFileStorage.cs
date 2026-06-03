namespace Frigorino.Domain.Interfaces
{
    // Vendor-neutral blob store. Lean by design: it only moves bytes. Content-type and length live
    // in the ListItem columns, so the store never has to persist or echo metadata — this keeps it
    // truly backend-agnostic. Keys are opaque (GUID-based) and not tied to the DB id, which lets the
    // upload happen before the row is persisted (with a compensating Delete on failure — sub-feature #2).
    public interface IFileStorage
    {
        Task<string> SaveAsync(Stream content, CancellationToken ct); // returns opaque key
        Task<Stream?> OpenAsync(string key, CancellationToken ct);     // null if the key is absent
        Task DeleteAsync(string key, CancellationToken ct);            // idempotent (no-op if absent)
    }
}
