using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    // Unit tests for the List aggregate's promotion-to-inventory state transitions.
    // Pending = Status && PromotionExpiryHandling != null && PromotionResolvedAt == null.
    public class ListAggregatePromotionTests
    {
        [Fact]
        public void ApplyPromotionSuggestion_PerishableItem_StampsCandidacyAndClearsResolved()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true);
            item.PromotionResolvedAt = DateTime.UtcNow.AddDays(-1); // stale resolution

            var expiry = new DateOnly(2026, 6, 20);
            var result = list.ApplyPromotionSuggestion(
                item.Id, ExpiryHandling.AiRecommendsShelfLife, expiry);

            Assert.True(result.IsSuccess);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, item.PromotionExpiryHandling);
            Assert.Equal(expiry, item.PromotionSuggestedExpiry);
            Assert.Null(item.PromotionResolvedAt);
        }

        [Fact]
        public void ApplyPromotionSuggestion_NonPerishable_LeavesCandidacyNull()
        {
            var list = NewList();
            var item = AddSeed(list, "Salt", status: true);

            var result = list.ApplyPromotionSuggestion(item.Id, handling: null, suggestedExpiry: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.PromotionExpiryHandling);
            Assert.Null(item.PromotionSuggestedExpiry);
            Assert.Null(item.PromotionResolvedAt);
        }

        [Fact]
        public void ApplyPromotionSuggestion_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ApplyPromotionSuggestion(999, ExpiryHandling.UserEntersFromPackage, null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void ToggleItemStatus_Uncheck_ClearsPromotionState()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true,
                sortOrder: SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap);
            item.PromotionExpiryHandling = ExpiryHandling.AiRecommendsShelfLife;
            item.PromotionSuggestedExpiry = new DateOnly(2026, 6, 20);
            item.PromotionResolvedAt = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);

            var result = list.ToggleItemStatus(item.Id); // checked -> unchecked

            Assert.True(result.IsSuccess);
            Assert.False(item.Status);
            Assert.Null(item.PromotionExpiryHandling);
            Assert.Null(item.PromotionSuggestedExpiry);
            Assert.Null(item.PromotionResolvedAt);
        }

        [Fact]
        public void ResolvePromotion_PendingItem_StampsResolvedAt()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true);
            item.PromotionExpiryHandling = ExpiryHandling.AiRecommendsShelfLife;

            var when = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
            var result = list.ResolvePromotion(item.Id, when);

            Assert.True(result.IsSuccess);
            Assert.Equal(when, item.PromotionResolvedAt);
            Assert.Equal(when, item.UpdatedAt);
        }

        [Fact]
        public void ResolvePromotion_AlreadyResolved_IsIdempotentNoOp()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true);
            var first = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
            item.PromotionResolvedAt = first;

            var result = list.ResolvePromotion(item.Id, new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc));

            Assert.True(result.IsSuccess);
            Assert.Equal(first, item.PromotionResolvedAt); // unchanged — first writer wins
        }

        [Fact]
        public void ResolvePromotion_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ResolvePromotion(999, DateTime.UtcNow);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- Helpers -------

        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        private static List NewList()
        {
            return new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = HouseholdId,
                CreatedByUserId = CreatorId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true,
            };
        }

        private int _nextItemId = 100;

        private ListItem AddSeed(List list, string text, bool status = false, int? sortOrder = null)
        {
            var item = new ListItem
            {
                Id = ++_nextItemId,
                ListId = list.Id,
                Text = text,
                Status = status,
                SortOrder = sortOrder ?? (status
                    ? SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap
                    : SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
            list.ListItems.Add(item);
            return item;
        }
    }
}
