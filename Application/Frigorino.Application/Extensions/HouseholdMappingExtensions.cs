using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Application.Extensions
{
    public static class HouseholdMappingExtensions
    {
        public static UserDto ToDto(this User user)
        {
            return new UserDto
            {
                ExternalId = user.ExternalId,
                Name = user.Name,
                Email = user.Email ?? ""
            };
        }

        public static HouseholdMemberDto ToMemberDto(this UserHousehold userHousehold)
        {
            return new HouseholdMemberDto
            {
                User = userHousehold.User.ToDto(),
                Role = userHousehold.Role,
                JoinedAt = userHousehold.JoinedAt
            };
        }
    }
}
