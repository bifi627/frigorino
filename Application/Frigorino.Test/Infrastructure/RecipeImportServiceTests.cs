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
    }
}
