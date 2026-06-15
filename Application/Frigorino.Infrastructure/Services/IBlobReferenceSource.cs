namespace Frigorino.Infrastructure.Services
{
    // A feature's contribution to the orphan sweep: the set of blob keys its rows currently
    // reference, for exactly one blob area. The sweep resolves the keyed storage for AreaName,
    // lists that folder, and reclaims blobs not in this set. Adding a blob-owning feature means
    // adding one of these (plus its area) — the sweep itself never changes.
    public interface IBlobReferenceSource
    {
        // Must match a BlobAreas constant — the DI key for this area's storage/maintenance.
        string AreaName { get; }

        // Every blob key (full-res AND thumbnail) referenced by live OR soft-deleted rows.
        // Soft-deleted rows keep their blobs for undo until they are purged, so they count.
        Task<ISet<string>> GetReferencedKeysAsync(CancellationToken ct);
    }
}
