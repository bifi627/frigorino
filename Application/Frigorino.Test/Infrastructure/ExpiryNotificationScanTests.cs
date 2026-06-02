using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Notifications;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Frigorino.Test.Infrastructure
{
    public class ExpiryNotificationScanTests
    {
        // Captures each SendDigestAsync call so the test can assert how many notifications were sent.
        private sealed class CapturingSender : INotificationSender
        {
            public List<(string UserId, ExpiryDigestNotification Notification)> Calls { get; } = new();
            public Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct)
            {
                Calls.Add((userId, notification));
                return Task.CompletedTask;
            }
        }

        // Simulates a send that fails (e.g. FCM transport error): SendDigestAsync throws every time.
        private sealed class ThrowingSender : INotificationSender
        {
            public int Attempts { get; private set; }
            public Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct)
            {
                Attempts++;
                throw new InvalidOperationException("send failed");
            }
        }

        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        // Seeds: one user, one household membership, one inventory (inventoryId=100, name="Fridge")
        // with one item expiring in `daysUntil`.
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

        private static ExpiryNotificationScan NewScan(ApplicationDbContext db, INotificationSender sender) =>
            new(db, sender, Options.Create(new MaintenanceSettings()), NullLogger<ExpiryNotificationScan>.Instance);

        [Fact]
        public async Task SendsOneNotification_AndWritesLedger_ForEligibleRecipient()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            var call = Assert.Single(sender.Calls);
            Assert.Equal("u1", call.UserId);
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.UserId == "u1" && d.InventoryId == 100 && d.SentOn == today));
        }

        [Fact]
        public async Task SkipsRecipient_WhenAlreadyDispatchedToday()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            db.NotificationDispatches.Add(NotificationDispatch.Create("u1", 100, today));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            Assert.Empty(sender.Calls);
        }

        [Fact]
        public async Task SkipsRecipient_WhenUserDisabled()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2, userEnabled: false);
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            Assert.Empty(sender.Calls);
        }

        [Fact]
        public async Task SkipsRecipient_WhenNoToken()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2, hasToken: false);
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            Assert.Empty(sender.Calls);
        }

        [Fact]
        public async Task SkipsItem_BeyondLeadWindow()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 30);
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            Assert.Empty(sender.Calls);
        }

        [Fact]
        public async Task WritesLedger_EvenWhenSendThrows_BecauseSlotClaimedFirst()
        {
            // Claim-slot-first: the ledger row is committed BEFORE the send, so a send that throws still
            // leaves a dispatch row (the accepted trade-off — the send is lost, not retried today).
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            var sender = new ThrowingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            Assert.Equal(1, sender.Attempts);
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.UserId == "u1" && d.InventoryId == 100 && d.SentOn == today));
        }

        [Fact]
        public async Task TwoInventories_ProduceTwoNotifications_AndTwoLedgerRows()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U", Email = "u@x.io" });
            db.Households.Add(new Household { Id = 10, Name = "H", CreatedByUserId = "u1" });
            db.UserHouseholds.Add(new UserHousehold { UserId = "u1", HouseholdId = 10, Role = HouseholdRole.Owner, IsActive = true });
            db.Inventories.Add(new Inventory { Id = 100, Name = "Fridge", HouseholdId = 10, CreatedByUserId = "u1", IsActive = true });
            db.Inventories.Add(new Inventory { Id = 101, Name = "Pantry", HouseholdId = 10, CreatedByUserId = "u1", IsActive = true });
            db.InventoryItems.Add(new InventoryItem { Id = 1000, InventoryId = 100, Text = "Milk", ExpiryDate = today.AddDays(1), IsActive = true });
            db.InventoryItems.Add(new InventoryItem { Id = 1001, InventoryId = 101, Text = "Flour", ExpiryDate = today.AddDays(2), IsActive = true });
            var userSettings = UserSettings.Create("u1");
            userSettings.SetExpiryNotifications(true, leadDays: 7);
            db.UserSettings.Add(userSettings);
            db.FcmTokens.Add(FcmToken.Create("u1", "tok-1"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            Assert.Equal(2, sender.Calls.Count);
            Assert.All(sender.Calls, c => Assert.Equal("u1", c.UserId));
            Assert.Equal(2, await db.NotificationDispatches.CountAsync(d => d.UserId == "u1" && d.SentOn == today));
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.InventoryId == 100));
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.InventoryId == 101));
        }

        [Fact]
        public async Task MutedInventory_IsExcluded_OtherInventoryStillSent()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U", Email = "u@x.io" });
            db.Households.Add(new Household { Id = 10, Name = "H", CreatedByUserId = "u1" });
            db.UserHouseholds.Add(new UserHousehold { UserId = "u1", HouseholdId = 10, Role = HouseholdRole.Owner, IsActive = true });
            db.Inventories.Add(new Inventory { Id = 100, Name = "Fridge", HouseholdId = 10, CreatedByUserId = "u1", IsActive = true });
            db.Inventories.Add(new Inventory { Id = 101, Name = "Pantry", HouseholdId = 10, CreatedByUserId = "u1", IsActive = true });
            db.InventoryItems.Add(new InventoryItem { Id = 1000, InventoryId = 100, Text = "Milk", ExpiryDate = today.AddDays(1), IsActive = true });
            db.InventoryItems.Add(new InventoryItem { Id = 1001, InventoryId = 101, Text = "Flour", ExpiryDate = today.AddDays(2), IsActive = true });
            var userSettings = UserSettings.Create("u1");
            userSettings.SetExpiryNotifications(true, leadDays: 7);
            db.UserSettings.Add(userSettings);
            db.FcmTokens.Add(FcmToken.Create("u1", "tok-1"));
            // Mute inventory 100 for user u1
            var mutedPref = UserInventoryNotificationSetting.Create("u1", 100);
            mutedPref.SetEnabled(false);
            db.UserInventoryNotificationSettings.Add(mutedPref);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var sender = new CapturingSender();

            await NewScan(db, sender).RunAsync(today, CancellationToken.None);

            // Only pantry (101) should produce a notification; fridge (100) is muted.
            var call = Assert.Single(sender.Calls);
            Assert.Equal("u1", call.UserId);
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.InventoryId == 101));
            Assert.Equal(0, await db.NotificationDispatches.CountAsync(d => d.InventoryId == 100));
        }
    }
}
