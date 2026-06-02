namespace Frigorino.Domain.Notifications
{
    // Composed, localized push payload. DeepLinkPath is a client-relative route (e.g. "/inventories")
    // the service worker opens on notification click.
    public sealed record ExpiryDigestNotification(string Title, string Body, string DeepLinkPath);
}
