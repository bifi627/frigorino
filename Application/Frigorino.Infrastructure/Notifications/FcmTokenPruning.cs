namespace Frigorino.Infrastructure.Notifications
{
    // Outcome of sending to one token. IsUnregistered = the provider says the token is
    // permanently invalid (Unregistered / InvalidArgument) and should be deleted; a plain
    // failure (transient) is left alone.
    public sealed record FcmSendOutcome(string Token, bool Success, bool IsUnregistered);

    public static class FcmTokenPruning
    {
        public static IReadOnlyList<string> SelectDeadTokens(IEnumerable<FcmSendOutcome> outcomes)
        {
            return outcomes
                .Where(o => o.IsUnregistered)
                .Select(o => o.Token)
                .ToList();
        }
    }
}
