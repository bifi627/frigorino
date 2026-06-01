using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class InventorySettings
    {
        public const int MinExpiryLeadDays = 0;
        public const int MaxExpiryLeadDays = 365;

        public int InventoryId { get; set; }

        // null = inherit the user-level default (resolved by the notification feature).
        public int? ExpiryLeadDays { get; set; }

        // Per-inventory enable. Default true so a newly-tracked inventory is discoverable
        // (a user can mute a noisy one without losing alerts elsewhere).
        public bool ExpiryNotificationsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Inventory Inventory { get; set; } = null!;

        public static InventorySettings Create(int inventoryId)
        {
            return new InventorySettings { InventoryId = inventoryId };
        }

        public Result SetExpiryLeadDays(int? days)
        {
            if (days is not null && (days < MinExpiryLeadDays || days > MaxExpiryLeadDays))
            {
                return Result.Fail(new Error($"Lead time must be between {MinExpiryLeadDays} and {MaxExpiryLeadDays} days.")
                    .WithMetadata("Property", nameof(ExpiryLeadDays)));
            }

            ExpiryLeadDays = days;
            return Result.Ok();
        }

        public void SetExpiryNotificationsEnabled(bool enabled)
        {
            ExpiryNotificationsEnabled = enabled;
        }
    }
}
