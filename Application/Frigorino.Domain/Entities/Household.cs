namespace Frigorino.Domain.Entities
{
    public class Household
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public User CreatedByUser { get; set; } = null!;
        public ICollection<UserHousehold> UserHouseholds { get; set; } = new List<UserHousehold>();
    }
}
