using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class GcsObjectNamingTests
    {
        [Fact]
        public void ToObjectName_PrependsPrefix()
        {
            Assert.Equal("list-items/abc123", GcsObjectNaming.ToObjectName("list-items", "abc123"));
        }

        [Fact]
        public void ToKey_StripsPrefix()
        {
            Assert.Equal("abc123", GcsObjectNaming.ToKey("list-items", "list-items/abc123"));
        }

        [Fact]
        public void ToKey_LeavesNonPrefixedNameUnchanged()
        {
            Assert.Equal("other/abc123", GcsObjectNaming.ToKey("list-items", "other/abc123"));
        }

        [Fact]
        public void RoundTrips()
        {
            var name = GcsObjectNaming.ToObjectName("list-items", "deadbeef");
            Assert.Equal("deadbeef", GcsObjectNaming.ToKey("list-items", name));
        }
    }
}
