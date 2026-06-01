using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class InventorySettingsTests
    {
        private const int InventoryId = 3;
        private const string CreatorId = "user-creator";

        [Fact]
        public void Create_DefaultsLeadDaysToNull()
        {
            var settings = InventorySettings.Create(InventoryId);

            Assert.Equal(InventoryId, settings.InventoryId);
            Assert.Null(settings.ExpiryLeadDays);
        }

        [Fact]
        public void SetExpiryLeadDays_Null_Succeeds_Inherit()
        {
            var settings = InventorySettings.Create(InventoryId);
            settings.SetExpiryLeadDays(5);

            var result = settings.SetExpiryLeadDays(null);

            Assert.True(result.IsSuccess);
            Assert.Null(settings.ExpiryLeadDays);
        }

        [Theory]
        [InlineData(InventorySettings.MinExpiryLeadDays)]
        [InlineData(InventorySettings.MaxExpiryLeadDays)]
        public void SetExpiryLeadDays_InBounds_Succeeds(int days)
        {
            var settings = InventorySettings.Create(InventoryId);

            var result = settings.SetExpiryLeadDays(days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, settings.ExpiryLeadDays);
        }

        [Theory]
        [InlineData(InventorySettings.MinExpiryLeadDays - 1)]
        [InlineData(InventorySettings.MaxExpiryLeadDays + 1)]
        public void SetExpiryLeadDays_OutOfBounds_Fails(int days)
        {
            var settings = InventorySettings.Create(InventoryId);

            var result = settings.SetExpiryLeadDays(days);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(InventorySettings.ExpiryLeadDays));
        }

        [Theory]
        [InlineData(CreatorId, HouseholdRole.Member, true)]   // creator
        [InlineData("other", HouseholdRole.Admin, true)]      // admin
        [InlineData("other", HouseholdRole.Owner, true)]      // owner
        [InlineData("other", HouseholdRole.Member, false)]    // non-creator member
        public void CanBeManagedBy_MatchesPolicy(string callerId, HouseholdRole role, bool expected)
        {
            var inventory = Inventory.Create("Pantry", null, 1, CreatorId).Value;

            Assert.Equal(expected, inventory.CanBeManagedBy(callerId, role));
        }

        [Fact]
        public void Create_DefaultsNotificationsEnabledTrue()
        {
            var settings = InventorySettings.Create(InventoryId);

            Assert.True(settings.ExpiryNotificationsEnabled);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SetExpiryNotificationsEnabled_Toggles(bool enabled)
        {
            var settings = InventorySettings.Create(InventoryId);

            settings.SetExpiryNotificationsEnabled(enabled);

            Assert.Equal(enabled, settings.ExpiryNotificationsEnabled);
        }
    }
}
