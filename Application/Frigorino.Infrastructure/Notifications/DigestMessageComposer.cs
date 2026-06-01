using Frigorino.Domain.Notifications;

namespace Frigorino.Infrastructure.Notifications
{
    public static class DigestMessageComposer
    {
        private const int MaxNamesInBody = 3;
        private const string DeepLinkPath = "/inventories";

        public static ExpiryDigestNotification Compose(DigestPlan plan, DateOnly today)
        {
            var german = string.Equals(plan.Language, "de", StringComparison.OrdinalIgnoreCase);
            var count = plan.Lines.Count;

            var title = german
                ? $"{count} Artikel laufen bald ab"
                : $"{count} item{(count == 1 ? "" : "s")} expiring soon";

            var named = plan.Lines
                .Take(MaxNamesInBody)
                .Select(l => $"{l.Text} {Phrase(l.DaysUntil, german)}");

            var body = string.Join(german ? ", " : ", ", named);

            var remaining = count - MaxNamesInBody;
            if (remaining > 0)
            {
                body += german ? $" und {remaining} weitere" : $", +{remaining} more";
            }

            return new ExpiryDigestNotification(title, body, DeepLinkPath);
        }

        private static string Phrase(int daysUntil, bool german)
        {
            if (daysUntil < 0)
            {
                return german ? "überfällig" : "overdue";
            }
            if (daysUntil == 0)
            {
                return german ? "heute" : "today";
            }
            if (daysUntil == 1)
            {
                return german ? "morgen" : "tomorrow";
            }
            return german ? $"in {daysUntil} Tagen" : $"in {daysUntil} days";
        }
    }
}
