namespace Frigorino.Features.Sync
{
    // Opaque change-detection token returned by the per-resource /revision endpoints. The client
    // treats `Rev` as a black box — it only compares it for equality between polls to decide whether
    // to refetch the real data query. Composed from the parent row's UpdatedAt (so a rename triggers
    // a refresh) plus the items' MAX(UpdatedAt) and active COUNT (so add / edit / reorder / soft-delete
    // all move it). Equality only — never parsed, never ordered.
    public sealed record RevisionResponse(string Rev)
    {
        // parentUpdatedAt is null for collection-level tokens (the calendar has no single parent row).
        // Empty item set → maxItemUpdatedAt null (encoded 0) and activeCount 0 → a stable token.
        public static RevisionResponse Compute(DateTime? parentUpdatedAt, DateTime? maxItemUpdatedAt, int activeCount)
        {
            var parentTicks = parentUpdatedAt?.Ticks ?? 0L;
            var maxTicks = maxItemUpdatedAt?.Ticks ?? 0L;
            return new RevisionResponse($"{parentTicks}.{maxTicks}.{activeCount}");
        }
    }
}
