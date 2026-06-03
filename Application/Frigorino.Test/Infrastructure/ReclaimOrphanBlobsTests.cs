using System.Runtime.CompilerServices;
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Infrastructure.Tasks;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
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

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task Run_ReclaimsOnly_UnreferencedAgedBlobs()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "",
                    Type = ListItemType.Image,
                    StorageKey = "ref-full",
                    ThumbnailStorageKey = "ref-thumb",
                    IsActive = true,
                });
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "",
                    Type = ListItemType.Image,
                    StorageKey = "ref-deleted",
                    ThumbnailStorageKey = null,
                    IsActive = false,
                });
                await db.SaveChangesAsync();
            }

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
            var settings = Options.Create(new MaintenanceSettings { OrphanBlobGraceHours = 24 });

            using var runDb = NewContext(dbName);
            var task = new ReclaimOrphanBlobs(
                runDb, storage, maintenance, settings, NullLogger<ReclaimOrphanBlobs>.Instance);

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
