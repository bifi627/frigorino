using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // Used when Firebase Admin isn't initialized (DevAuth bypass, integration tests). Lets the
    // scan run end-to-end and logs what *would* have been sent, without a real FirebaseApp.
    public class LogOnlyNotificationSender : INotificationSender
    {
        private readonly ILogger<LogOnlyNotificationSender> _logger;

        public LogOnlyNotificationSender(ILogger<LogOnlyNotificationSender> logger)
        {
            _logger = logger;
        }

        public Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct)
        {
            _logger.LogInformation(
                "[LogOnlyNotificationSender] Would send to {UserId}: \"{Title}\" — {Body}",
                userId, notification.Title, notification.Body);
            return Task.CompletedTask;
        }
    }
}
