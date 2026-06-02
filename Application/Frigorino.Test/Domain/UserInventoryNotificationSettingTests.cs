using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class UserInventoryNotificationSettingTests
    {
        [Fact]
        public void Create_Defaults_To_Enabled_And_Inherited_Lead()
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);

            Assert.Equal("user-1", s.UserId);
            Assert.Equal(42, s.InventoryId);
            Assert.True(s.Enabled);
            Assert.Null(s.LeadDays);
        }

        [Fact]
        public void SetEnabled_Toggles_Flag()
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);
            s.SetEnabled(false);
            Assert.False(s.Enabled);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(365)]
        [InlineData(null)]
        public void SetLeadDays_Accepts_In_Range_And_Null(int? days)
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);
            var result = s.SetLeadDays(days);
            Assert.True(result.IsSuccess);
            Assert.Equal(days, s.LeadDays);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(366)]
        public void SetLeadDays_Rejects_Out_Of_Range(int days)
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);
            var result = s.SetLeadDays(days);
            Assert.True(result.IsFailed);
            Assert.Equal(nameof(UserInventoryNotificationSetting.LeadDays),
                result.Errors[0].Metadata["Property"]);
        }
    }
}
