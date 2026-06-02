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

            var anyInvalidArgument = false;
            var outcomes = new List<FcmSendOutcome>(tokens.Count);
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                var code = r.Exception?.MessagingErrorCode;
                if (code is MessagingErrorCode.InvalidArgument)
                {
                    anyInvalidArgument = true;
                }

                var isDeadToken = FcmTokenPruning.IsPermanentlyDeadToken(code);
                outcomes.Add(new FcmSendOutcome(tokens[i], r.IsSuccess, isDeadToken));
            }

            if (anyInvalidArgument)
            {
                // InvalidArgument is FCM's malformed-message error, not a per-token death. It fans out
                // to every token when the payload is bad (e.g. data over the 4KB limit), so we prune
                // nothing for it — wiping the device set would mask a payload bug as token churn.
                _logger.LogWarning(
                    "FCM reported InvalidArgument for user {UserId} — likely a malformed/oversized payload; not pruning any token.",
                    userId);
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
