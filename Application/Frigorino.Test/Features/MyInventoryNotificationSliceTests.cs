using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Inventories.Notifications;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class MyInventoryNotificationSliceTests
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

        /// <summary>Seeds a household, an active membership, and an active inventory; returns inventoryId.</summary>
        private static async Task<int> SeedAsync(TestApplicationDbContext db, string userId, int householdId)
        {
            db.Households.Add(new Household { Id = householdId, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId,
                HouseholdId = householdId,
                Role = HouseholdRole.Member,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
            });
            var inventory = new Inventory
            {
                Name = "Fridge",
                HouseholdId = householdId,
                CreatedByUserId = userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Inventories.Add(inventory);
            await db.SaveChangesAsync();
            return inventory.Id;
        }

        [Fact]
        public async Task Get_NoRow_ReturnsDefaultSubscribed()
        {
            using var db = NewContext();
            var inventoryId = await SeedAsync(db, "u1", householdId: 1);

            var result = await GetMyInventoryNotificationEndpoint.Handle(
                householdId: 1, inventoryId, UserNamed("u1"), db, CancellationToken.None);

            var ok = Assert.IsType<Ok<MyInventoryNotificationResponse>>(result.Result);
            Assert.True(ok.Value!.Enabled);
            Assert.Null(ok.Value.LeadDays);
        }

        [Fact]
        public async Task Put_CreatesRow_PersistedAndEchoed()
        {
            using var db = NewContext();
            var inventoryId = await SeedAsync(db, "u1", householdId: 1);

            var result = await UpdateMyInventoryNotificationEndpoint.Handle(
                householdId: 1,
                inventoryId,
                new UpdateMyInventoryNotificationRequest(Enabled: false, LeadDays: 14),
                UserNamed("u1"),
                db,
                CancellationToken.None);

            var ok = Assert.IsType<Ok<MyInventoryNotificationResponse>>(result.Result);
            Assert.False(ok.Value!.Enabled);
            Assert.Equal(14, ok.Value.LeadDays);

            // Persisted in DB
            var row = await db.UserInventoryNotificationSettings.SingleAsync();
            Assert.Equal("u1", row.UserId);
            Assert.Equal(inventoryId, row.InventoryId);
            Assert.False(row.Enabled);
            Assert.Equal(14, row.LeadDays);
        }

        [Fact]
        public async Task Put_ThenGet_RoundTrips()
        {
            using var db = NewContext();
            var inventoryId = await SeedAsync(db, "u1", householdId: 1);

            await UpdateMyInventoryNotificationEndpoint.Handle(
                householdId: 1,
                inventoryId,
                new UpdateMyInventoryNotificationRequest(Enabled: false, LeadDays: 5),
                UserNamed("u1"),
                db,
                CancellationToken.None);

            db.ChangeTracker.Clear();

            var getResult = await GetMyInventoryNotificationEndpoint.Handle(
                householdId: 1, inventoryId, UserNamed("u1"), db, CancellationToken.None);

            var ok = Assert.IsType<Ok<MyInventoryNotificationResponse>>(getResult.Result);
            Assert.False(ok.Value!.Enabled);
            Assert.Equal(5, ok.Value.LeadDays);
        }

        [Fact]
        public async Task Get_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var inventoryId = await SeedAsync(db, "u1", householdId: 1);

            // u2 has no membership in household 1
            var result = await GetMyInventoryNotificationEndpoint.Handle(
                householdId: 1, inventoryId, UserNamed("u2"), db, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task Put_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var inventoryId = await SeedAsync(db, "u1", householdId: 1);

            // u2 has no membership in household 1
            var result = await UpdateMyInventoryNotificationEndpoint.Handle(
                householdId: 1,
                inventoryId,
                new UpdateMyInventoryNotificationRequest(Enabled: true, LeadDays: null),
                UserNamed("u2"),
                db,
                CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task Put_LeadDays_OutOfRange_ReturnsValidationProblem()
        {
            using var db = NewContext();
            var inventoryId = await SeedAsync(db, "u1", householdId: 1);

            var result = await UpdateMyInventoryNotificationEndpoint.Handle(
                householdId: 1,
                inventoryId,
                new UpdateMyInventoryNotificationRequest(Enabled: true, LeadDays: 400),
                UserNamed("u1"),
                db,
                CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
        }
    }
}
