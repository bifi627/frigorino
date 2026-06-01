using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class HouseholdSettings
    {
        public const int DefaultCheckedItemRetentionDays = 30;
        public const int MinRetentionDays = 1;
        public const int MaxRetentionDays = 365;

        public int HouseholdId { get; set; }
        public int CheckedItemRetentionDays { get; set; } = DefaultCheckedItemRetentionDays;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Household Household { get; set; } = null!;

        public static HouseholdSettings Create(int householdId)
        {
            return new HouseholdSettings { HouseholdId = householdId };
        }

        public Result SetCheckedItemRetentionDays(int days)
        {
            if (days < MinRetentionDays || days > MaxRetentionDays)
            {
                return Result.Fail(new Error($"Retention must be between {MinRetentionDays} and {MaxRetentionDays} days.")
                    .WithMetadata("Property", nameof(CheckedItemRetentionDays)));
            }

            CheckedItemRetentionDays = days;
            return Result.Ok();
        }
    }
}
