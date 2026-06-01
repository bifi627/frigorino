using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class NotificationPersistenceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task UserSettings_PersistsNotificationFields()
        {
            using var db = NewContext();
            var settings = UserSettings.Create("user-1");
            settings.SetExpiryNotifications(enabled: true, leadDays: 5);
            db.UserSettings.Add(settings);
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var loaded = await db.UserSettings.SingleAsync(s => s.UserId == "user-1");

            Assert.True(loaded.ExpiryNotificationsEnabled);
            Assert.Equal(5, loaded.ExpiryLeadDays);
        }

        [Fact]
        public async Task InventorySettings_PersistsEnabledFlag()
        {
            using var db = NewContext();
            var settings = InventorySettings.Create(42);
            settings.SetExpiryNotificationsEnabled(false);
            db.InventorySettings.Add(settings);
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var loaded = await db.InventorySettings.SingleAsync(s => s.InventoryId == 42);

            Assert.False(loaded.ExpiryNotificationsEnabled);
        }

        [Fact]
        public async Task FcmToken_SaveChanges_StampsTimestamps()
        {
            using var db = NewContext();
            var token = FcmToken.Create("user-1", "tok-1");
            db.FcmTokens.Add(token);
            await db.SaveChangesAsync();

            Assert.NotEqual(default, token.CreatedAt);
            Assert.NotEqual(default, token.LastSeenAt);
        }

        [Fact]
        public async Task NotificationDispatch_Roundtrips()
        {
            using var db = NewContext();
            var dispatch = NotificationDispatch.Create("user-1", 7, new DateOnly(2026, 6, 1));
            db.NotificationDispatches.Add(dispatch);
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var loaded = await db.NotificationDispatches.SingleAsync();

            Assert.Equal("user-1", loaded.UserId);
            Assert.Equal(7, loaded.HouseholdId);
            Assert.Equal(new DateOnly(2026, 6, 1), loaded.SentOn);
        }
    }
}
