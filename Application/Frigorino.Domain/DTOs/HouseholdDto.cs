using Frigorino.Domain.Entities;

namespace Frigorino.Domain.DTOs
{
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

    // Member management DTOs
    public class AddMemberRequest
    {
        public string Email { get; set; } = string.Empty;
        public HouseholdRole? Role { get; set; } = HouseholdRole.Member;
    }

    public class UpdateMemberRoleRequest
    {
        public HouseholdRole Role { get; set; }
    }
}
