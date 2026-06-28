using System.Net;
using FluentResults;

namespace Frigorino.Infrastructure.Services
{
    public class RecipeImportService
    {
        public const long MaxResponseBytes = 15 * 1024 * 1024;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _http;

        public RecipeImportService(HttpClient http) => _http = http;

        // ponytail: protected ctor is the IT test seam (StubRecipeImportService overrides ImportAsync);
        // avoids a one-impl interface that the spec deliberately omits.
        protected RecipeImportService() => _http = null!;

        public static RecipeImportService CreateDefault() => new(BuildGuardedClient());

        internal static HttpClient BuildGuardedClient()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectCallback = RecipeImportConnect.ConnectAsync,
            };
            var client = new HttpClient(handler) { Timeout = RequestTimeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Frigorino/1.0; +recipe-import)");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            return client;
        }

        public virtual async Task<Result<ImportedRecipe>> ImportAsync(string url, CancellationToken ct)
        {
            if (!RecipeImportUrl.TryParseHttpUrl(url, out var uri))
            {
                return Fail("invalid_url", "Enter a valid http(s) URL.");
            }

            string html;
            try
            {
                using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return Fail("fetch_failed", $"The page returned status {(int)resp.StatusCode}.");
                }
                var mediaType = resp.Content.Headers.ContentType?.MediaType;
                if (mediaType is null || !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    return Fail("fetch_failed", "The URL did not return an HTML page.");
                }
                if (resp.Content.Headers.ContentLength is > MaxResponseBytes)
                {
                    return Fail("page_too_large", "The page is too large to import.");
                }
                html = await ReadCappedAsync(resp, ct);
            }
            catch (ResponseTooLargeException)
            {
                return Fail("page_too_large", "The page is too large to import.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                return Fail("fetch_failed", "Could not fetch the page.");
            }

            var parsed = JsonLdRecipeParser.Parse(html);
            return parsed is null
                ? Fail("no_recipe_found", "Could not find a recipe on this page.")
                : Result.Ok(parsed);
        }

        // Pulls the cover image through the SAME guarded client as the page (SSRF check + redirect
        // guard + caps apply identically). Best-effort: every failure mode returns Fail and the slice
        // skips the cover — no error code, the SPA never sees it.
        public virtual async Task<Result<byte[]>> FetchImageAsync(string imageUrl, CancellationToken ct)
        {
            if (!RecipeImportUrl.TryParseHttpUrl(imageUrl, out var uri))
            {
                return Result.Fail<byte[]>("Image URL is not an http(s) URL.");
            }

            try
            {
                // Per-request Accept overrides the client's default text/html (correct for an image
                // request, and avoids a 406 from stricter hosts). Limited to the formats IImageProcessor
                // decodes, so content-negotiating CDNs (Cloudinary f_auto) hand back something we can re-encode.
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.Accept.ParseAdd("image/jpeg,image/png,image/webp");
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return Result.Fail<byte[]>($"The image returned status {(int)resp.StatusCode}.");
                }
                var mediaType = resp.Content.Headers.ContentType?.MediaType;
                if (mediaType is null || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Fail<byte[]>("The URL did not return an image.");
                }
                if (resp.Content.Headers.ContentLength is > MaxResponseBytes)
                {
                    return Result.Fail<byte[]>("The image is too large.");
                }
                return Result.Ok(await ReadCappedBytesAsync(resp, ct));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                return Result.Fail<byte[]>("Could not fetch the image.");
            }
        }

        // ponytail: assumes UTF-8 — charset sniffing skipped; revisit if non-UTF-8 sites mis-parse.
        private static async Task<string> ReadCappedAsync(HttpResponseMessage resp, CancellationToken ct)
            => System.Text.Encoding.UTF8.GetString(await ReadCappedBytesAsync(resp, ct));

        private static async Task<byte[]> ReadCappedBytesAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                {
                    throw new ResponseTooLargeException();
                }
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        private static Result<ImportedRecipe> Fail(string code, string message)
            => Result.Fail<ImportedRecipe>(new Error(message).WithMetadata("code", code));

        // Distinguishes the streaming size-cap abort from a genuine network IOException so the
        // caller can report page_too_large rather than the generic fetch_failed.
        private sealed class ResponseTooLargeException : IOException
        {
        }
    }
}
