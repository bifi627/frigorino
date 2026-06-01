using FirebaseAdmin.Messaging;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Notifications;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // FCM adapter. Sends a data-only message (the service worker renders it) to every active
    // token the user has, then prunes any the provider reports as permanently unregistered.
    public class FcmNotificationSender : INotificationSender
    {
        private readonly ApplicationDbContext _db;
        private readonly FirebaseMessaging _messaging;
        private readonly ILogger<FcmNotificationSender> _logger;

        public FcmNotificationSender(
            ApplicationDbContext db,
            FirebaseMessaging messaging,
            ILogger<FcmNotificationSender> logger)
        {
            _db = db;
            _messaging = messaging;
            _logger = logger;
        }

        public async Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct)
        {
            var tokens = await _db.FcmTokens
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .ToListAsync(ct);

            if (tokens.Count == 0)
            {
                return;
            }

            var message = new MulticastMessage
            {
                Tokens = tokens,
                // Data-only: the SW composes/shows the notification (avoids duplicate auto-display).
                Data = new Dictionary<string, string>
                {
                    ["title"] = notification.Title,
                    ["body"] = notification.Body,
                    ["link"] = notification.DeepLinkPath,
                },
            };

            var response = await _messaging.SendEachForMulticastAsync(message, ct);

            var outcomes = new List<FcmSendOutcome>(tokens.Count);
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                var unregistered = r.Exception?.MessagingErrorCode
                    is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument;
                outcomes.Add(new FcmSendOutcome(tokens[i], r.IsSuccess, unregistered));
            }

            var dead = FcmTokenPruning.SelectDeadTokens(outcomes);
            if (dead.Count > 0)
            {
                await _db.FcmTokens.Where(t => dead.Contains(t.Token)).ExecuteDeleteAsync(ct);
                _logger.LogInformation("Pruned {Count} unregistered FCM token(s) for user {UserId}.", dead.Count, userId);
            }
        }
    }
}
