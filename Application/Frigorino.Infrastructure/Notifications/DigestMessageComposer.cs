using Frigorino.Domain.Notifications;

namespace Frigorino.Infrastructure.Notifications
{
    public static class DigestMessageComposer
    {
        private const int MaxNamesInBody = 3;

        public static ExpiryDigestNotification Compose(DigestPlan plan)
        {
            var german = string.Equals(plan.Language, "de", StringComparison.OrdinalIgnoreCase);
            var count = plan.Lines.Count;

            var title = german
                ? $"{plan.InventoryName}: {count} Artikel laufen bald ab"
                : $"{plan.InventoryName}: {count} item{(count == 1 ? "" : "s")} expiring soon";

            var named = plan.Lines
                .Take(MaxNamesInBody)
                .Select(l => $"{l.Text} {Phrase(l.DaysUntil, german)}");

            var body = string.Join(", ", named);

            var remaining = count - MaxNamesInBody;
            if (remaining > 0)
            {
                body += german ? $" und {remaining} weitere" : $", +{remaining} more";
            }

            // Deep-link straight to the inventory detail page so a click lands on the items.
            // Carry the target household id — the recipient's active household may differ, and
            // the inventory request is household-scoped, so the SPA switches to it on landing.
            var deepLinkPath = $"/inventories/{plan.InventoryId}/view?householdId={plan.HouseholdId}";

            return new ExpiryDigestNotification(title, body, deepLinkPath);
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
