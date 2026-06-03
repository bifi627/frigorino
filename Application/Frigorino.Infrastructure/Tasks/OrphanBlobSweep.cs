using Frigorino.Domain.Interfaces;

namespace Frigorino.Infrastructure.Tasks
{
    // Pure orphan-reclamation decision: which blob keys are safe to delete. A blob is reclaimable
    // when no ListItems row references it AND it is older than the grace period (which protects an
    // in-flight upload whose row is not yet committed). Kept free of EF and IO so it is unit-testable
    // without a database or a storage backend.
    public static class OrphanBlobSweep
    {
        public static List<string> SelectReclaimableKeys(
            IEnumerable<StoredBlob> blobs,
            ISet<string> referencedKeys,
            DateTimeOffset now,
            TimeSpan gracePeriod)
        {
            var cutoff = now - gracePeriod;
            var reclaimable = new List<string>();
            foreach (var blob in blobs)
            {
                if (referencedKeys.Contains(blob.Key))
                {
                    continue;
                }

                if (blob.CreatedAt > cutoff)
                {
                    continue; // too fresh — may be an in-flight upload
                }

                reclaimable.Add(blob.Key);
            }

            return reclaimable;
        }
    }
}
