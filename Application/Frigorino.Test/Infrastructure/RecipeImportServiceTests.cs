using System.Net;
using System.Net.Http.Headers;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class RecipeImportServiceTests
    {
        private static string? Code(FluentResults.IResultBase r)
            => r.Errors[0].Metadata.TryGetValue("code", out var c) ? c?.ToString() : null;

        [Fact]
        public async Task Rejects_non_http_url_with_invalid_url()
        {
            var service = RecipeImportService.CreateDefault();
            var result = await service.ImportAsync("ftp://example.com/x", CancellationToken.None);
            Assert.True(result.IsFailed);
            Assert.Equal("invalid_url", Code(result));
        }

        [Theory]
        [InlineData("http://127.0.0.1/recipe")]
        [InlineData("http://169.254.169.254/latest/meta-data")]
        [InlineData("http://10.0.0.5/")]
        public async Task Blocks_private_targets_as_fetch_failed(string url)
        {
            var service = RecipeImportService.CreateDefault();
            var result = await service.ImportAsync(url, CancellationToken.None);
            Assert.True(result.IsFailed);
            Assert.Equal("fetch_failed", Code(result));
        }

        [Fact]
        public async Task Reports_page_too_large_when_content_length_exceeds_cap()
        {
            // The Content-Length header pre-check aborts before reading the body.
            var content = new StringContent("<html>x</html>", System.Text.Encoding.UTF8, "text/html");
            content.Headers.ContentLength = RecipeImportService.MaxResponseBytes + 1;
            var service = new RecipeImportService(StubClient(content));

            var result = await service.ImportAsync("https://example.com/recipe", CancellationToken.None);

            Assert.True(result.IsFailed);
            Assert.Equal("page_too_large", Code(result));
        }

        [Fact]
        public async Task Reports_page_too_large_when_streamed_body_exceeds_cap()
        {
            // Non-seekable stream over the cap and no Content-Length, so the limit is only hit
            // mid-stream in ReadCappedAsync (the streaming path, not the header pre-check).
            var content = new StreamContent(new FixedLengthStream(RecipeImportService.MaxResponseBytes + 1));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            var service = new RecipeImportService(StubClient(content));

            var result = await service.ImportAsync("https://example.com/recipe", CancellationToken.None);

            Assert.True(result.IsFailed);
            Assert.Equal("page_too_large", Code(result));
        }

        [Fact]
        public async Task FetchImage_returns_bytes_for_image_response()
        {
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            var service = new RecipeImportService(StubClient(content));

            var result = await service.FetchImageAsync("https://example.com/img.jpg", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(bytes, result.Value);
        }

        [Fact]
        public async Task FetchImage_rejects_non_image_content_type()
        {
            var content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html");
            var service = new RecipeImportService(StubClient(content));

            var result = await service.FetchImageAsync("https://example.com/notimage", CancellationToken.None);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task FetchImage_rejects_non_http_url()
        {
            var service = RecipeImportService.CreateDefault();
            var result = await service.FetchImageAsync("ftp://example.com/img.jpg", CancellationToken.None);
            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task FetchImage_fails_when_streamed_body_exceeds_cap()
        {
            var content = new StreamContent(new FixedLengthStream(RecipeImportService.MaxResponseBytes + 1));
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            var service = new RecipeImportService(StubClient(content));

            var result = await service.FetchImageAsync("https://example.com/big.jpg", CancellationToken.None);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public async Task ImportAsync_caches_successful_parse_and_skips_second_fetch()
        {
            const string html =
                "<html><head><script type=\"application/ld+json\">" +
                "{\"@context\":\"https://schema.org\",\"@type\":\"Recipe\",\"name\":\"Cake\",\"recipeIngredient\":[\"x\"]}" +
                "</script></head></html>";
            var handler = new CountingHandler(html);
            var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var service = new RecipeImportService(new HttpClient(handler), cache);

            var first = await service.ImportAsync("https://example.com/r", CancellationToken.None);
            var second = await service.ImportAsync("https://example.com/r", CancellationToken.None);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal("Cake", second.Value.Name);
            Assert.Equal(1, handler.Count); // second call served from cache, no 2nd fetch
        }

        private sealed class CountingHandler(string html) : HttpMessageHandler
        {
            public int Count { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Count++;
                var content = new StringContent(html, System.Text.Encoding.UTF8, "text/html");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }
        }

        private static HttpClient StubClient(HttpContent content)
            => new(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(response);
        }

        // Yields `length` zero bytes from a non-seekable stream (so StreamContent emits no
        // Content-Length) without allocating the full buffer.
        private sealed class FixedLengthStream(long length) : Stream
        {
            private long _remaining = length;

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining <= 0)
                {
                    return 0;
                }
                var n = (int)Math.Min(count, _remaining);
                Array.Clear(buffer, offset, n);
                _remaining -= n;
                return n;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
