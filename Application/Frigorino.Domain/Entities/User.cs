namespace Frigorino.Domain.Entities
{
    public class User
    {
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<UserHousehold> UserHouseholds { get; set; } = new List<UserHousehold>();
        public ICollection<Household> CreatedHouseholds { get; set; } = new List<Household>();
    }
}
