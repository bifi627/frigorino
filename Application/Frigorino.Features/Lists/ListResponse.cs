using Frigorino.Domain.Entities;

namespace Frigorino.Features.Lists
{
    public sealed record ListResponse(
        int Id,
        string Name,
        string? Description,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        ListCreatorResponse CreatedByUser,
        int UncheckedCount,
        int CheckedCount)
    {
        public static ListResponse From(List list, User creator, int uncheckedCount, int checkedCount)
        {
            return new ListResponse(
                list.Id,
                list.Name,
                list.Description,
                list.HouseholdId,
                list.CreatedAt,
                list.UpdatedAt,
                new ListCreatorResponse(creator.ExternalId, creator.Name, creator.Email),
                uncheckedCount,
                checkedCount);
        }
    }

    public sealed record ListCreatorResponse(
        string ExternalId,
        string? Name,
        string? Email);
}
