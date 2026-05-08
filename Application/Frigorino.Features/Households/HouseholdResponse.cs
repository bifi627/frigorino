using Frigorino.Domain.Entities;

namespace Frigorino.Features.Households
{
    public sealed record HouseholdResponse(
        int Id,
        string Name,
        string? Description,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string CreatedByUserId,
        HouseholdRole CurrentUserRole)
    {
        public static HouseholdResponse From(Household h, HouseholdRole role)
        {
            return new HouseholdResponse(
                h.Id, h.Name, h.Description, h.CreatedAt, h.UpdatedAt, h.CreatedByUserId, role);
        }
    }
}
