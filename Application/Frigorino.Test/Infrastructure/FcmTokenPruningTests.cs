using FirebaseAdmin.Messaging;
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

        [Theory]
        [InlineData(MessagingErrorCode.Unregistered)]
        [InlineData(MessagingErrorCode.SenderIdMismatch)]
        public void IsPermanentlyDeadToken_IsTrue_ForTokenSpecificDeathSignals(MessagingErrorCode code)
        {
            Assert.True(FcmTokenPruning.IsPermanentlyDeadToken(code));
        }

        [Theory]
        [InlineData(MessagingErrorCode.InvalidArgument)]
        [InlineData(MessagingErrorCode.Internal)]
        [InlineData(MessagingErrorCode.Unavailable)]
        public void IsPermanentlyDeadToken_IsFalse_ForPayloadOrTransientErrors(MessagingErrorCode code)
        {
            Assert.False(FcmTokenPruning.IsPermanentlyDeadToken(code));
        }

        [Fact]
        public void IsPermanentlyDeadToken_IsFalse_ForNull()
        {
            Assert.False(FcmTokenPruning.IsPermanentlyDeadToken(null));
        }
    }
}
