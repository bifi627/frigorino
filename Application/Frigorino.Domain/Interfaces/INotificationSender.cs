using Frigorino.Domain.Notifications;

namespace Frigorino.Domain.Interfaces
{
    // Delivery boundary. The FCM adapter (Infrastructure) resolves the user's tokens, sends the
    // payload to each, and prunes any the provider reports as permanently invalid.
    public interface INotificationSender
    {
        Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct);
    }
}
