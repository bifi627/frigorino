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
