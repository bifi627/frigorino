using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class ExpiryNotificationScanTests
    {
        // Captures enqueued work items so the test can assert how many sends were scheduled.
        private sealed class CapturingQueue : IBackgroundTaskQueue
        {
            public List<Func<IServiceProvider, CancellationToken, Task>> Items { get; } = new();
            public bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work)
            {
                Items.Add(work);
                return true;
            }
        }

        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        // Seeds: one user, one household membership, one inventory with one item expiring in `daysUntil`.
        private static async Task SeedAsync(
            ApplicationDbContext db, DateOnly today, int daysUntil,
            bool userEnabled = true, bool hasToken = true)
        {
            db.Users.Add(new User { ExternalId = "u1", Name = "U", Email = "u@x.io" });
            db.Households.Add(new Household { Id = 10, Name = "H", CreatedByUserId = "u1" });
            db.UserHouseholds.Add(new UserHousehold { UserId = "u1", HouseholdId = 10, Role = HouseholdRole.Owner, IsActive = true });
            db.Inventories.Add(new Inventory { Id = 100, Name = "Fridge", HouseholdId = 10, CreatedByUserId = "u1", IsActive = true });
            db.InventoryItems.Add(new InventoryItem { Id = 1000, InventoryId = 100, Text = "Milk", ExpiryDate = today.AddDays(daysUntil), IsActive = true });

            var userSettings = UserSettings.Create("u1");
            userSettings.SetExpiryNotifications(userEnabled, leadDays: 3);
            db.UserSettings.Add(userSettings);

            if (hasToken)
            {
                db.FcmTokens.Add(FcmToken.Create("u1", "tok-1"));
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        private static ExpiryNotificationScan NewScan(ApplicationDbContext db, IBackgroundTaskQueue queue) =>
            new(db, queue, NullLogger<ExpiryNotificationScan>.Instance);

        [Fact]
        public async Task EnqueuesOneSend_AndWritesLedger_ForEligibleRecipient()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Single(queue.Items);
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.UserId == "u1" && d.HouseholdId == 10 && d.SentOn == today));
        }

        [Fact]
        public async Task SkipsRecipient_WhenAlreadyDispatchedToday()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            db.NotificationDispatches.Add(NotificationDispatch.Create("u1", 10, today));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task SkipsRecipient_WhenUserDisabled()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2, userEnabled: false);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task SkipsRecipient_WhenNoToken()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2, hasToken: false);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task SkipsItem_BeyondLeadWindow()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 30);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }
    }
}
