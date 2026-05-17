using Microsoft.AspNetCore.StaticFiles;

namespace Frigorino.Web.Middlewares
{
    public sealed class CompressedAwareContentTypeProvider : IContentTypeProvider
    {
        private static readonly string[] _compressedExtensions = [".br", ".gz"];

        private readonly FileExtensionContentTypeProvider _inner = new();

        public bool TryGetContentType(string subpath, out string contentType)
        {
            foreach (var ext in _compressedExtensions)
            {
                if (subpath.EndsWith(ext, StringComparison.Ordinal))
                {
                    var underlying = subpath[..^ext.Length];
                    if (_inner.TryGetContentType(underlying, out contentType!))
                    {
                        return true;
                    }
                }
            }
            return _inner.TryGetContentType(subpath, out contentType!);
        }
    }
}
