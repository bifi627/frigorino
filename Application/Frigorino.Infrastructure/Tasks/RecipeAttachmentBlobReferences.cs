using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    // Referenced-key source for the recipe-attachment blob area: every full-res + thumbnail key
    // across ALL RecipeAttachment rows (active AND soft-deleted — soft-deleted rows keep their blob
    // for undo until they are purged). Scoped (depends on the request/scope DbContext).
    public sealed class RecipeAttachmentBlobReferences : IBlobReferenceSource
    {
        private readonly ApplicationDbContext _dbContext;

        public RecipeAttachmentBlobReferences(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public string AreaName => BlobAreas.RecipeAttachment;

        public async Task<ISet<string>> GetReferencedKeysAsync(CancellationToken ct)
        {
            var keyPairs = await _dbContext.RecipeAttachments
                .Select(a => new { a.StorageKey, a.ThumbnailStorageKey })
                .ToListAsync(ct);

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in keyPairs)
            {
                if (!string.IsNullOrEmpty(pair.StorageKey))
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
