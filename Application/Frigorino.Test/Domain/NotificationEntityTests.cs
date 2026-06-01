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
        public void NotificationDispatch_Create_SetsKey()
        {
            var sentOn = new DateOnly(2026, 6, 1);

            var dispatch = NotificationDispatch.Create("user-1", householdId: 7, sentOn);

            Assert.Equal("user-1", dispatch.UserId);
            Assert.Equal(7, dispatch.HouseholdId);
            Assert.Equal(sentOn, dispatch.SentOn);
        }
    }
}
