using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class NotificationEntityTests
    {
        [Fact]
        public void FcmToken_Create_SetsUserAndToken()
        {
            var token = FcmToken.Create("user-1", "device-token-abc");

            Assert.Equal("user-1", token.UserId);
            Assert.Equal("device-token-abc", token.Token);
        }

        [Fact]
        public void NotificationDispatch_Create_Sets_Keys()
        {
            var d = NotificationDispatch.Create("user-1", 99, new DateOnly(2026, 6, 2));

            Assert.Equal("user-1", d.UserId);
            Assert.Equal(99, d.InventoryId);
            Assert.Equal(new DateOnly(2026, 6, 2), d.SentOn);
        }
    }
}
