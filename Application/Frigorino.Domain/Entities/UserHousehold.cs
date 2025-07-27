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
    }

    public enum HouseholdRole
    {
        Member = 0,
        Admin = 1,
        Owner = 2
    }
}
