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
    }
}
