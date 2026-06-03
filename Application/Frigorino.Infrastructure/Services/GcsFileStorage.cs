using System.Net;
using System.Runtime.CompilerServices;
using Frigorino.Domain.Interfaces;
using Google;
using Google.Cloud.Storage.V1;

namespace Frigorino.Infrastructure.Services
{
    // Production blob backend on Firebase Cloud Storage (GCS). Hot path mirrors LocalFileStorage's
    // contract: bare GUID keys, and the served content-type lives in the DB row (so the stored
    // object's content-type is irrelevant). All objects are written under a fixed prefix so the
    // orphan sweep is bucket-safe. StorageClient is thread-safe -> registered as a singleton.
    public sealed class GcsFileStorage : IFileStorage, IFileStorageMaintenance
    {
        private readonly StorageClient _client;
        private readonly string _bucket;
        private readonly string _prefix;

        public GcsFileStorage(StorageClient client, string bucket, string prefix)
        {
            _client = client;
            _bucket = bucket;
            _prefix = prefix;
        }

        public async Task<string> SaveAsync(Stream content, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N");
            await _client.UploadObjectAsync(
                _bucket,
                GcsObjectNaming.ToObjectName(_prefix, key),
                contentType: "application/octet-stream",
                source: content,
                options: null,
                cancellationToken: ct);
            return key;
        }

        public async Task<Stream?> OpenAsync(string key, CancellationToken ct)
        {
            var ms = new MemoryStream();
            try
            {
                await _client.DownloadObjectAsync(
                    _bucket,
                    GcsObjectNaming.ToObjectName(_prefix, key),
                    ms,
                    options: null,
                    cancellationToken: ct);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                await ms.DisposeAsync();
                return null;
            }

            ms.Position = 0;
            return ms;
        }

        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            try
            {
                await _client.DeleteObjectAsync(
                    _bucket,
                    GcsObjectNaming.ToObjectName(_prefix, key),
                    options: null,
                    cancellationToken: ct);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // idempotent — already gone
            }
        }

        public async IAsyncEnumerable<StoredBlob> ListAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var obj in _client.ListObjectsAsync(_bucket, _prefix).WithCancellation(ct))
            {
                // TimeCreatedDateTimeOffset is the non-deprecated creation timestamp on the resolved
                // Google.Apis.Storage; falls back to MinValue (always past the grace cutoff -> eligible) if absent.
                yield return new StoredBlob(
                    GcsObjectNaming.ToKey(_prefix, obj.Name),
                    obj.TimeCreatedDateTimeOffset ?? DateTimeOffset.MinValue);
            }
        }
    }
}
