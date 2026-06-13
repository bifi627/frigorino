using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Lists.Items;
using Frigorino.Features.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class CreateItemSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        private static async Task<int> SeedListAsync(TestApplicationDbContext db, string userId, int householdId)
        {
            db.Households.Add(new Household { Id = householdId, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId, HouseholdId = householdId, Role = HouseholdRole.Member,
                IsActive = true, JoinedAt = DateTime.UtcNow,
            });
            var list = new List
            {
                Name = "Groceries", HouseholdId = householdId, CreatedByUserId = userId,
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            return list.Id;
        }

        [Fact]
        public async Task Post_WithStructuredQuantity_PersistsQuantityAndSkipsExtraction()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            var trigger = A.Fake<IQuantityExtractionTrigger>();

            var result = await CreateItemEndpoint.Handle(
                householdId: 1, listId,
                new CreateItemRequest("Milk", null, new QuantityDto(2m, QuantityUnit.Liter)),
                UserNamed("u1"), db, trigger, CancellationToken.None);

            var created = Assert.IsType<Created<ListItemResponse>>(result.Result);
            Assert.False(created.Value!.ExtractionPending);

            var row = await db.ListItems.SingleAsync();
            Assert.Equal("Milk", row.Text);
            Assert.Equal(2m, row.QuantityValue);
            Assert.Equal(QuantityUnit.Liter, row.QuantityUnit);

            A.CallTo(() => trigger.OnItemRouted(
                A<int>._, A<int>._, A<int>._, A<ItemTextAnalysis>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_WithoutQuantity_RoutesThroughExtraction()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            var trigger = A.Fake<IQuantityExtractionTrigger>();

            var result = await CreateItemEndpoint.Handle(
                householdId: 1, listId,
                new CreateItemRequest("2 apples", null),
                UserNamed("u1"), db, trigger, CancellationToken.None);

            Assert.IsType<Created<ListItemResponse>>(result.Result);
            var row = await db.ListItems.SingleAsync();
            Assert.Null(row.QuantityValue);
            A.CallTo(() => trigger.OnItemRouted(
                A<int>._, A<int>._, A<int>._, A<ItemTextAnalysis>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Post_WithQuantity_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            var trigger = A.Fake<IQuantityExtractionTrigger>();

            var result = await CreateItemEndpoint.Handle(
                householdId: 1, listId,
                new CreateItemRequest("Milk", null, new QuantityDto(2m, QuantityUnit.Liter)),
                UserNamed("intruder"), db, trigger, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
            Assert.Empty(await db.ListItems.ToListAsync());
        }
    }
}
