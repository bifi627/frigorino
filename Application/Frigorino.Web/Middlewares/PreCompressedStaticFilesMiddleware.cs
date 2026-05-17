using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Frigorino.Web.Middlewares
{
    public sealed class PreCompressedStaticFilesMiddleware
    {
        private static readonly Encoding _brotli = new(".br", "br");
        private static readonly Encoding _gzip = new(".gz", "gzip");

        private readonly RequestDelegate _next;
        private readonly IFileProvider _fileProvider;

        public PreCompressedStaticFilesMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;
            _fileProvider = env.WebRootFileProvider;
        }

        public Task InvokeAsync(HttpContext context)
        {
            if (TryRewriteToCompressedSibling(context, out var encoding))
            {
                var headers = context.Response.Headers;
                headers.ContentEncoding = encoding.Header;
                AppendVaryAcceptEncoding(headers);
            }

            return _next(context);
        }

        private bool TryRewriteToCompressedSibling(HttpContext context, out Encoding encoding)
        {
            encoding = default!;

            var method = context.Request.Method;
            if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            {
                return false;
            }

            var path = context.Request.Path.Value;
            if (string.IsNullOrEmpty(path)
                || path.EndsWith(_brotli.Extension, StringComparison.Ordinal)
                || path.EndsWith(_gzip.Extension, StringComparison.Ordinal))
            {
                return false;
            }

            var acceptEncoding = context.Request.Headers.AcceptEncoding;
            if (acceptEncoding.Count == 0)
            {
                return false;
            }

            // Prefer Brotli — better compression at decode cost browsers already pay for everyone else.
            if (AcceptsEncoding(acceptEncoding, _brotli.Header)
                && SiblingExists(path, _brotli.Extension))
            {
                encoding = _brotli;
                context.Request.Path = new PathString(path + _brotli.Extension);
                return true;
            }

            if (AcceptsEncoding(acceptEncoding, _gzip.Header)
                && SiblingExists(path, _gzip.Extension))
            {
                encoding = _gzip;
                context.Request.Path = new PathString(path + _gzip.Extension);
                return true;
            }

            return false;
        }

        private bool SiblingExists(string path, string siblingExtension)
        {
            // IFileProvider rejects path traversal — relative segments outside the root return non-existent files.
            var info = _fileProvider.GetFileInfo(path + siblingExtension);
            return info.Exists && !info.IsDirectory;
        }

        private static bool AcceptsEncoding(StringValues acceptEncoding, string encoding)
        {
            foreach (var value in acceptEncoding)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                foreach (var raw in value.Split(','))
                {
                    var token = raw.AsSpan().Trim();
                    var semi = token.IndexOf(';');
                    var name = (semi >= 0 ? token[..semi] : token).Trim();
                    if (name.Equals(encoding, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void AppendVaryAcceptEncoding(IHeaderDictionary headers)
        {
            var vary = headers.Vary;
            foreach (var value in vary)
            {
                if (!string.IsNullOrEmpty(value)
                    && value.Contains(HeaderNames.AcceptEncoding, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);
        }

        private readonly record struct Encoding(string Extension, string Header);
    }
}
