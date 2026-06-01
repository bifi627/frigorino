using FirebaseAdmin.Messaging;

namespace Frigorino.Infrastructure.Notifications
{
    // Outcome of sending to one token. IsUnregistered = the provider says the token is
    // permanently invalid (Unregistered / SenderIdMismatch) and should be deleted; a plain
    // failure (transient) is left alone.
    public sealed record FcmSendOutcome(string Token, bool Success, bool IsUnregistered);

    public static class FcmTokenPruning
    {
        // Classifies a per-token FCM error code as a permanent, token-specific death signal.
        // ONLY Unregistered (the app was uninstalled / token rotated) and SenderIdMismatch (the
        // token belongs to a different sender) mean *this token* is dead. InvalidArgument is NOT a
        // token death — it is FCM's malformed-message error, which fans out to EVERY token when the
        // payload is bad (e.g. data over the 4KB limit); pruning on it would wipe the whole device set.
        public static bool IsPermanentlyDeadToken(MessagingErrorCode? code)
        {
            return code is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch;
        }

        public static IReadOnlyList<string> SelectDeadTokens(IEnumerable<FcmSendOutcome> outcomes)
        {
            return outcomes
                .Where(o => o.IsUnregistered)
                .Select(o => o.Token)
                .ToList();
        }
    }
}
