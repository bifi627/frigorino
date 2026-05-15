using System.Linq.Expressions;
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

        // EF-translatable projection used by read slices (GetList, GetLists). Lifted out of
        // both queries so the shape stays in one place; expression body must stay simple
        // enough for EF to translate (no method calls outside Count, no captured variables).
        public static readonly Expression<Func<List, ListResponse>> ToProjection = l => new ListResponse(
            l.Id,
            l.Name,
            l.Description,
            l.HouseholdId,
            l.CreatedAt,
            l.UpdatedAt,
            new ListCreatorResponse(l.CreatedByUser.ExternalId, l.CreatedByUser.Name, l.CreatedByUser.Email),
            l.ListItems.Count(i => i.IsActive && !i.Status),
            l.ListItems.Count(i => i.IsActive && i.Status));
    }

    public sealed record ListCreatorResponse(
        string ExternalId,
        string Name,
        string? Email);
}
