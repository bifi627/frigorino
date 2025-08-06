using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Application.Extensions
{
    public static class ListMappingExtensions
    {
        public static ListDto ToDto(this List list)
        {
            return new ListDto
            {
                Id = list.Id,
                Name = list.Name,
                Description = list.Description,
                HouseholdId = list.HouseholdId,
                CreatedAt = list.CreatedAt,
                UpdatedAt = list.UpdatedAt,
                CreatedByUser = list.CreatedByUser.ToDto(),
                CheckedCount = list.ListItems.Count(li => li.Status),
                UncheckedCount = list.ListItems.Count(li => !li.Status),

            };
        }

        public static List ToEntity(this CreateListRequest request, int householdId, string userId)
        {
            return new List
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                HouseholdId = householdId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static void UpdateFromRequest(this List list, UpdateListRequest request)
        {
            list.Name = request.Name.Trim();
            list.Description = request.Description?.Trim();
            list.UpdatedAt = DateTime.UtcNow;
        }
    }
}
