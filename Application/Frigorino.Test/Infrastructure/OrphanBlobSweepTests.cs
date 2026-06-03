using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Tasks;

namespace Frigorino.Test.Infrastructure
{
    public class OrphanBlobSweepTests
    {
        private static readonly DateTimeOffset Now = new(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Grace = TimeSpan.FromHours(24);

        [Fact]
        public void ReferencedKey_IsKept_EvenWhenOld()
        {
            var blobs = new[] { new StoredBlob("ref", Now.AddDays(-10)) };
            var referenced = new HashSet<string> { "ref" };

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Empty(result);
        }

        [Fact]
        public void UnreferencedAgedKey_IsReclaimed()
        {
            var blobs = new[] { new StoredBlob("orphan", Now.AddDays(-2)) };
            var referenced = new HashSet<string>();

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Equal(new[] { "orphan" }, result);
        }

        [Fact]
        public void UnreferencedFreshKey_IsKept_WithinGracePeriod()
        {
            var blobs = new[] { new StoredBlob("fresh", Now.AddHours(-1)) };
            var referenced = new HashSet<string>();

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Empty(result);
        }

        [Fact]
        public void MixedSet_ReclaimsOnlyUnreferencedAged()
        {
            var blobs = new[]
            {
                new StoredBlob("ref-full", Now.AddDays(-5)),
                new StoredBlob("ref-thumb", Now.AddDays(-5)),
                new StoredBlob("orphan-old", Now.AddDays(-5)),
                new StoredBlob("orphan-fresh", Now.AddMinutes(-5)),
            };
            var referenced = new HashSet<string> { "ref-full", "ref-thumb" };

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Equal(new[] { "orphan-old" }, result);
        }

        [Fact]
        public void EmptyInputs_ReturnEmpty()
        {
            var result = OrphanBlobSweep.SelectReclaimableKeys(
                Array.Empty<StoredBlob>(), new HashSet<string>(), Now, Grace);

            Assert.Empty(result);
        }
    }
}
