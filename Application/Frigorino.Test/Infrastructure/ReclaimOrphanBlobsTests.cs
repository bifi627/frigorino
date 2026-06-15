using System.Runtime.CompilerServices;
using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Infrastructure.Services;
using Frigorino.Infrastructure.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Frigorino.Test.Infrastructure
{
    public class ReclaimOrphanBlobsTests
    {
        // Returns a fixed set of blobs as an async stream (FakeItEasy is awkward for IAsyncEnumerable).
        private sealed class StubMaintenance : IFileStorageMaintenance
        {
            private readonly IReadOnlyList<StoredBlob> _blobs;

            public StubMaintenance(IReadOnlyList<StoredBlob> blobs)
            {
                _blobs = blobs;
            }

            public async IAsyncEnumerable<StoredBlob> ListAsync([EnumeratorCancellation] CancellationToken ct)
            {
                foreach (var blob in _blobs)
                {
                    yield return blob;
                }

                await Task.CompletedTask;
            }
        }

        // Supplies the referenced-key set for one area without touching a database.
        private sealed class StubReferenceSource : IBlobReferenceSource
        {
            private readonly ISet<string> _keys;

            public StubReferenceSource(string area, ISet<string> keys)
            {
                AreaName = area;
                _keys = keys;
            }

            public string AreaName { get; }

            public Task<ISet<string>> GetReferencedKeysAsync(CancellationToken ct) => Task.FromResult(_keys);
        }

        [Fact]
        public async Task Run_ReclaimsOnly_UnreferencedAgedBlobs_InTheArea()
        {
            var old = DateTimeOffset.UtcNow.AddDays(-2);
            var fresh = DateTimeOffset.UtcNow.AddMinutes(-5);
            var maintenance = new StubMaintenance(new[]
            {
                new StoredBlob("ref-full", old),
                new StoredBlob("ref-thumb", old),
                new StoredBlob("ref-deleted", old),
                new StoredBlob("orphan-old", old),
                new StoredBlob("orphan-fresh", fresh),
            });
            var storage = A.Fake<IFileStorage>();

            // Referenced set: full+thumb of a live row, plus a soft-deleted row's full-res key.
            var referenced = new HashSet<string>(StringComparer.Ordinal) { "ref-full", "ref-thumb", "ref-deleted" };
            var source = new StubReferenceSource(BlobAreas.ListItem, referenced);

            var services = new ServiceCollection();
            services.AddKeyedSingleton<IFileStorage>(BlobAreas.ListItem, storage);
            services.AddKeyedSingleton<IFileStorageMaintenance>(BlobAreas.ListItem, maintenance);
            using var sp = services.BuildServiceProvider();

            var settings = Options.Create(new MaintenanceSettings { OrphanBlobGraceHours = 24 });
            var task = new ReclaimOrphanBlobs(
                sp, new[] { source }, settings, NullLogger<ReclaimOrphanBlobs>.Instance);

            await task.Run();

            A.CallTo(() => storage.DeleteAsync("orphan-old", A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => storage.DeleteAsync("orphan-fresh", A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync("ref-full", A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync("ref-thumb", A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync("ref-deleted", A<CancellationToken>._))
                .MustNotHaveHappened();
        }
    }
}
