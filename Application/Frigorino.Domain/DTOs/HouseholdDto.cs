using Frigorino.Domain.Entities;

namespace Frigorino.Domain.DTOs
{
    public class HouseholdDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserDto CreatedByUser { get; set; } = null!;
        public HouseholdRole CurrentUserRole { get; set; }
        public int MemberCount { get; set; }
        public List<HouseholdMemberDto> Members { get; set; } = new();
    }

    public class HouseholdMemberDto
    {
        public UserDto User { get; set; } = null!;
        public HouseholdRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class UserDto
    {
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CreateHouseholdRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateHouseholdRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
