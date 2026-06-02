using FluentResults;

namespace Frigorino.Domain.Entities
{
    // Per-user, per-inventory expiry-notification preference. A MISSING row is the default:
    // subscribed (Enabled = true) and inheriting the user's global lead time (LeadDays = null).
    // A user opts OUT of an inventory by setting Enabled = false, and may override the lead
    // time for that one inventory. Replaces the former household-wide InventorySettings flags.
    public class UserInventoryNotificationSetting
    {
        public const int MinLeadDays = 0;
        public const int MaxLeadDays = 365;

        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int InventoryId { get; set; }

        // Default true: a member is subscribed to every inventory in their households until
        // they explicitly mute it.
        public bool Enabled { get; set; } = true;

        // null = inherit the user's global ExpiryLeadDays.
        public int? LeadDays { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Inventory Inventory { get; set; } = null!;

        public static UserInventoryNotificationSetting Create(string userId, int inventoryId)
        {
            return new UserInventoryNotificationSetting
            {
                UserId = userId,
                InventoryId = inventoryId,
            };
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }

        public Result SetLeadDays(int? days)
        {
            if (days is not null && (days < MinLeadDays || days > MaxLeadDays))
            {
                return Result.Fail(new Error($"Lead time must be between {MinLeadDays} and {MaxLeadDays} days.")
                    .WithMetadata("Property", nameof(LeadDays)));
            }

            LeadDays = days;
            return Result.Ok();
        }
    }
}
