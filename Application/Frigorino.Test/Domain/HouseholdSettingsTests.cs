using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class HouseholdSettingsTests
    {
        private const int HouseholdId = 7;

        [Fact]
        public void Create_DefaultsRetentionToDefaultConstant()
        {
            var settings = HouseholdSettings.Create(HouseholdId);

            Assert.Equal(HouseholdId, settings.HouseholdId);
            Assert.Equal(HouseholdSettings.DefaultCheckedItemRetentionDays, settings.CheckedItemRetentionDays);
        }

        [Theory]
        [InlineData(HouseholdSettings.MinRetentionDays)]
        [InlineData(30)]
        [InlineData(HouseholdSettings.MaxRetentionDays)]
        public void SetCheckedItemRetentionDays_InBounds_Succeeds(int days)
        {
            var settings = HouseholdSettings.Create(HouseholdId);

            var result = settings.SetCheckedItemRetentionDays(days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, settings.CheckedItemRetentionDays);
        }

        [Theory]
        [InlineData(HouseholdSettings.MinRetentionDays - 1)]
        [InlineData(HouseholdSettings.MaxRetentionDays + 1)]
        public void SetCheckedItemRetentionDays_OutOfBounds_Fails(int days)
        {
            var settings = HouseholdSettings.Create(HouseholdId);

            var result = settings.SetCheckedItemRetentionDays(days);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(HouseholdSettings.CheckedItemRetentionDays));
        }

        [Theory]
        [InlineData(HouseholdRole.Owner, true)]
        [InlineData(HouseholdRole.Admin, true)]
        [InlineData(HouseholdRole.Member, false)]
        public void CanManageSettings_MatchesRolePolicy(HouseholdRole role, bool expected)
        {
            Assert.Equal(expected, role.CanManageSettings());
        }
    }
}
