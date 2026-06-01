using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class FcmTokenPruningTests
    {
        [Fact]
        public void SelectsOnlyTokensReportedUnregistered()
        {
            var results = new[]
            {
                new FcmSendOutcome("tok-ok", Success: true, IsUnregistered: false),
                new FcmSendOutcome("tok-dead", Success: false, IsUnregistered: true),
                new FcmSendOutcome("tok-transient", Success: false, IsUnregistered: false),
            };

            var dead = FcmTokenPruning.SelectDeadTokens(results);

            Assert.Equal(new[] { "tok-dead" }, dead);
        }
    }
}
