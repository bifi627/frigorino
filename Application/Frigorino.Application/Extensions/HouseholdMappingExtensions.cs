using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Application.Extensions
{
    public static class HouseholdMappingExtensions
    {
        public static HouseholdDto ToDto(this Household household, HouseholdRole currentUserRole)
        {
            return new HouseholdDto
            {
                Id = household.Id,
                Name = household.Name,
                Description = household.Description,
                CreatedAt = household.CreatedAt,
                UpdatedAt = household.UpdatedAt,
                CreatedByUser = household.CreatedByUser.ToDto(),
                CurrentUserRole = currentUserRole,
                MemberCount = household.UserHouseholds.Count(x => x.IsActive),
                Members = household.UserHouseholds
                    .Where(x => x.IsActive)
                    .Select(x => x.ToMemberDto())
                    .ToList()
            };
        }

        public static HouseholdDto ToDto(this UserHousehold userHousehold)
        {
            return new HouseholdDto
            {
                Id = userHousehold.Household.Id,
                Name = userHousehold.Household.Name,
                Description = userHousehold.Household.Description,
                CreatedAt = userHousehold.Household.CreatedAt,
                UpdatedAt = userHousehold.Household.UpdatedAt,
                CreatedByUser = userHousehold.Household.CreatedByUser.ToDto(),
                CurrentUserRole = userHousehold.Role,
                MemberCount = userHousehold.Household.UserHouseholds.Count(x => x.IsActive),
                Members = userHousehold.Household.UserHouseholds
                    .Where(x => x.IsActive)
                    .Select(x => x.ToMemberDto())
                    .ToList()
            };
        }

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

        public static Household ToEntity(this CreateHouseholdRequest request, string userId)
        {
            return new Household
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static void UpdateFromRequest(this Household household, UpdateHouseholdRequest request)
        {
            household.Name = request.Name.Trim();
            household.Description = request.Description?.Trim();
            household.UpdatedAt = DateTime.UtcNow;
        }
    }
}
