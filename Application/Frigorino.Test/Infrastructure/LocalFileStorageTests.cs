using System.Text;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class LocalFileStorageTests : IDisposable
    {
        private readonly string _root =
            Path.Combine(Path.GetTempPath(), "frigorino-test-" + Guid.NewGuid().ToString("N"));

        private LocalFileStorage NewStorage() => new(_root);

        [Fact]
        public async Task SaveOpenDelete_RoundTrips()
        {
            var storage = NewStorage();
            var bytes = Encoding.UTF8.GetBytes("hello blob");

            string key;
            using (var input = new MemoryStream(bytes))
            {
                key = await storage.SaveAsync(input, CancellationToken.None);
            }
            Assert.False(string.IsNullOrWhiteSpace(key));

            using (var opened = await storage.OpenAsync(key, CancellationToken.None))
            {
                Assert.NotNull(opened);
                using var ms = new MemoryStream();
                await opened!.CopyToAsync(ms);
                Assert.Equal(bytes, ms.ToArray());
            }

            await storage.DeleteAsync(key, CancellationToken.None);
            await using var afterDelete = await storage.OpenAsync(key, CancellationToken.None);
            Assert.Null(afterDelete);
        }

        [Fact]
        public async Task OpenAsync_UnknownKey_ReturnsNull()
        {
            var storage = NewStorage();
            var result = await storage.OpenAsync("does-not-exist", CancellationToken.None);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAsync_UnknownKey_DoesNotThrow()
        {
            var storage = NewStorage();
            await storage.DeleteAsync("does-not-exist", CancellationToken.None);
        }

        [Fact]
        public async Task SaveAsync_GeneratesDistinctKeys()
        {
            var storage = NewStorage();
            using var a = new MemoryStream(new byte[] { 1 });
            using var b = new MemoryStream(new byte[] { 2 });

            var keyA = await storage.SaveAsync(a, CancellationToken.None);
            var keyB = await storage.SaveAsync(b, CancellationToken.None);

            Assert.NotEqual(keyA, keyB);
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../escape")]
        [InlineData("sub/dir")]
        public async Task OpenAsync_TraversalKey_Throws(string key)
        {
            var storage = NewStorage();
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await storage.OpenAsync(key, CancellationToken.None));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
