using Frigorino.Domain.Interfaces;

namespace Frigorino.Infrastructure.Services
{
    // Dev/test blob backend: writes each blob to {root}/{guid}. Keys are GUIDs, deliberately not
    // tied to the DB id, so an upload can happen before the row is persisted (sub-feature #2 adds a
    // compensating Delete on persist failure). Stateless apart from the root path → singleton.
    public sealed class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(string root)
        {
            _root = root;
            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveAsync(Stream content, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N");
            var path = PathFor(key);
            await using var file = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous);
            await content.CopyToAsync(file, ct);
            return key;
        }

        public Task<Stream?> OpenAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var path = PathFor(key);
            if (!File.Exists(path))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.Asynchronous);
            return Task.FromResult<Stream?>(stream);
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return Task.CompletedTask;
        }

        // Resolve the key to a path INSIDE the root. Any key that escapes the root (separators,
        // "..", absolute paths) is rejected explicitly rather than relying on accidental OS behavior.
        private string PathFor(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Storage key is required.", nameof(key));
            }

            // Keys are flat GUIDs by design — no subdirectories. Reject separators up front so an
            // in-root subpath (e.g. "sub/dir") is treated as invalid, not silently created.
            if (key.Contains('/') || key.Contains('\\'))
            {
                throw new ArgumentException($"Invalid storage key: '{key}'", nameof(key));
            }

            var fullRoot = Path.GetFullPath(_root);
            var fullPath = Path.GetFullPath(Path.Combine(fullRoot, key));
            var rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid storage key: '{key}'", nameof(key));
            }

            return fullPath;
        }
    }
}
