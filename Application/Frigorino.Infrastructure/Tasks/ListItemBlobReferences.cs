using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    // Referenced-key source for the list-item blob area: every full-res + thumbnail key across ALL
    // ListItems rows (active AND soft-deleted — soft-deleted items keep their blob for undo until
    // they are purged). Scoped (depends on the request/scope DbContext).
    public sealed class ListItemBlobReferences : IBlobReferenceSource
    {
        private readonly ApplicationDbContext _dbContext;

        public ListItemBlobReferences(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public string AreaName => BlobAreas.ListItem;

        public async Task<ISet<string>> GetReferencedKeysAsync(CancellationToken ct)
        {
            var keyPairs = await _dbContext.ListItems
                .Where(li => li.StorageKey != null)
                .Select(li => new { li.StorageKey, li.ThumbnailStorageKey })
                .ToListAsync(ct);

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in keyPairs)
            {
                if (pair.StorageKey is not null)
                {
                    referenced.Add(pair.StorageKey);
                }

                if (pair.ThumbnailStorageKey is not null)
                {
                    referenced.Add(pair.ThumbnailStorageKey);
                }
            }

            return referenced;
        }
    }
}
