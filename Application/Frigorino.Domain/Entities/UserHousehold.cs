using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class UserHousehold
    {
        public string UserId { get; set; } = string.Empty;
        public int HouseholdId { get; set; }
        public HouseholdRole Role { get; set; } = HouseholdRole.Member;
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public User User { get; set; } = null!;
        public Household Household { get; set; } = null!;

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public static Result<UserHousehold> CreateMembership(string targetUserId, int householdId, HouseholdRole role)
        {
            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                return Result.Fail<UserHousehold>(
                    new Error("Target user id is required.")
                        .WithMetadata("Property", nameof(UserId)));
            }

            return Result.Ok(new UserHousehold
            {
                UserId = targetUserId,
                HouseholdId = householdId,
                Role = role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true,
            });
        }
    }

    public enum HouseholdRole
    {
        Member = 0,
        Admin = 1,
        Owner = 2
    }
}
