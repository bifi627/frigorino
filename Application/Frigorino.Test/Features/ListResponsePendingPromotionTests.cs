using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists;

namespace Frigorino.Test.Features
{
    // Verifies the EF-translatable projection counts only checked, candidate, unresolved items
    // checked-off within the promote-candidacy window.
    public class ListResponsePendingPromotionTests
    {
        [Fact]
        public void ToProjection_CountsOnlyCheckedCandidateUnresolvedInWindow()
        {
            var list = new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = 42,
                CreatedByUserId = "u1",
                CreatedByUser = new User { ExternalId = "u1", Name = "U", Email = "u@e.com" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
            };
            // Pending: checked + candidate + unresolved, fresh.
            list.ListItems.Add(Item(1, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null));
            // Not pending: resolved.
            list.ListItems.Add(Item(2, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: DateTime.UtcNow));
            // Not pending: not a candidate (handling null).
            list.ListItems.Add(Item(3, status: true, handling: null, resolvedAt: null));
            // Not pending: unchecked.
            list.ListItems.Add(Item(4, status: false, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null));
            // Pending: candidate just inside the window (UtcNow - 6d, window is 7d).
            list.ListItems.Add(Item(5, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null,
                updatedAt: DateTime.UtcNow.AddDays(-(ListItem.PromoteWindowDays - 1))));
            // Not pending: candidate aged out of the window (UtcNow - 8d).
            list.ListItems.Add(Item(6, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null,
                updatedAt: DateTime.UtcNow.AddDays(-(ListItem.PromoteWindowDays + 1))));

            var promoteCutoff = DateTime.UtcNow.AddDays(-ListItem.PromoteWindowDays);
            var projected = ListResponse.ToProjection(promoteCutoff).Compile()(list);

            Assert.Equal(2, projected.PendingPromotionCount);
        }

        private static ListItem Item(int id, bool status, ExpiryHandling? handling, DateTime? resolvedAt, DateTime? updatedAt = null)
        {
            return new ListItem
            {
                Id = id,
                ListId = 1,
                Text = "x",
                Status = status,
                IsActive = true,
                PromotionExpiryHandling = handling,
                PromotionResolvedAt = resolvedAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = updatedAt ?? DateTime.UtcNow,
            };
        }
    }
}
