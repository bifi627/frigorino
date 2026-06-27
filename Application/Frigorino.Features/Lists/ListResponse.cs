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
        int CheckedCount,
        int PendingPromotionCount)
    {
        public static ListResponse From(List list, User creator, int uncheckedCount, int checkedCount, int pendingPromotionCount)
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
                checkedCount,
                pendingPromotionCount);
        }

        // EF-translatable projection used by read slices (GetList, GetLists). A factory (not a static
        // field) so the per-request promote-candidacy cutoff (UtcNow - PromoteWindowDays) is baked in
        // as a captured constant EF binds to a query parameter; the body must stay EF-translatable
        // (no method calls outside Count). Keep the pending predicate in lockstep with UpdateList +
        // GetPendingPromotions (see ListItem.PromotionExpiryHandling comment).
        public static Expression<Func<List, ListResponse>> ToProjection(DateTime promoteCutoff) => l => new ListResponse(
            l.Id,
            l.Name,
            l.Description,
            l.HouseholdId,
            l.CreatedAt,
            l.UpdatedAt,
            new ListCreatorResponse(l.CreatedByUser.ExternalId, l.CreatedByUser.Name, l.CreatedByUser.Email),
            l.ListItems.Count(i => i.IsActive && !i.Status),
            l.ListItems.Count(i => i.IsActive && i.Status),
            l.ListItems.Count(i => i.IsActive && i.Status && i.PromotionExpiryHandling != null && i.PromotionResolvedAt == null && i.UpdatedAt >= promoteCutoff));
    }

    public sealed record ListCreatorResponse(
        string ExternalId,
        string Name,
        string? Email);
}
