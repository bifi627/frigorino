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
    }
}
