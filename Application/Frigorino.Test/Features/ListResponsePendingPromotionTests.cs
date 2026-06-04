using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists;

namespace Frigorino.Test.Features
{
    // Verifies the EF-translatable projection counts only checked, candidate, unresolved items.
    public class ListResponsePendingPromotionTests
    {
        [Fact]
        public void ToProjection_CountsOnlyCheckedCandidateUnresolved()
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
            // Pending: checked + candidate + unresolved.
            list.ListItems.Add(Item(1, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null));
            // Not pending: resolved.
            list.ListItems.Add(Item(2, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: DateTime.UtcNow));
            // Not pending: not a candidate (handling null).
            list.ListItems.Add(Item(3, status: true, handling: null, resolvedAt: null));
            // Not pending: unchecked.
            list.ListItems.Add(Item(4, status: false, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null));

            var projected = ListResponse.ToProjection.Compile()(list);

            Assert.Equal(1, projected.PendingPromotionCount);
        }

        private static ListItem Item(int id, bool status, ExpiryHandling? handling, DateTime? resolvedAt)
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
                UpdatedAt = DateTime.UtcNow,
            };
        }
    }
}
