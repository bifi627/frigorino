using Frigorino.Domain.Entities;
using Xunit;

namespace Frigorino.Test.Domain
{
    public class InventorySettingsTests
    {
        [Fact]
        public void Create_Sets_InventoryId()
        {
            var settings = InventorySettings.Create(7);

            Assert.Equal(7, settings.InventoryId);
        }
    }
}
