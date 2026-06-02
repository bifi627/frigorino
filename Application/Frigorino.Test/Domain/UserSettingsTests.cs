using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class UserSettingsTests
    {
        private const string UserId = "user-1";

        [Fact]
        public void Create_SetsUserId_AndNullLanguage()
        {
            var settings = UserSettings.Create(UserId);

            Assert.Equal(UserId, settings.UserId);
            Assert.Null(settings.Language);
        }

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public void SetLanguage_Supported_Succeeds(string lang)
        {
            var settings = UserSettings.Create(UserId);

            var result = settings.SetLanguage(lang);

            Assert.True(result.IsSuccess);
            Assert.Equal(lang, settings.Language);
        }

        [Fact]
        public void SetLanguage_Null_Succeeds_AndClears()
        {
            var settings = UserSettings.Create(UserId);
            settings.SetLanguage("de");

            var result = settings.SetLanguage(null);

            Assert.True(result.IsSuccess);
            Assert.Null(settings.Language);
        }

        [Fact]
        public void SetLanguage_Unsupported_Fails_WithLanguageProperty()
        {
            var settings = UserSettings.Create(UserId);

            var result = settings.SetLanguage("fr");

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(UserSettings.Language));
        }

        [Fact]
        public void Create_DefaultsNotificationsOffAndLeadDaysToDefault()
        {
            var settings = UserSettings.Create(UserId);

            Assert.False(settings.ExpiryNotificationsEnabled);
            Assert.Equal(UserSettings.DefaultExpiryLeadDays, settings.ExpiryLeadDays);
        }

        [Theory]
        [InlineData(UserSettings.MinExpiryLeadDays, true)]
        [InlineData(UserSettings.MaxExpiryLeadDays, true)]
        [InlineData(UserSettings.MinExpiryLeadDays, false)]
        public void SetExpiryNotifications_InBounds_Succeeds(int days, bool enabled)
        {
            var settings = UserSettings.Create(UserId);

            var result = settings.SetExpiryNotifications(enabled: enabled, leadDays: days);

            Assert.True(result.IsSuccess);
            Assert.Equal(enabled, settings.ExpiryNotificationsEnabled);
            Assert.Equal(days, settings.ExpiryLeadDays);
        }

        [Theory]
        [InlineData(UserSettings.MinExpiryLeadDays - 1)]
        [InlineData(UserSettings.MaxExpiryLeadDays + 1)]
        public void SetExpiryNotifications_OutOfBounds_Fails(int days)
        {
            var settings = UserSettings.Create(UserId);

            var result = settings.SetExpiryNotifications(enabled: true, leadDays: days);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p)
                && (string?)p == nameof(UserSettings.ExpiryLeadDays));
            Assert.False(settings.ExpiryNotificationsEnabled);
            Assert.Equal(UserSettings.DefaultExpiryLeadDays, settings.ExpiryLeadDays);
        }
    }
}
