using Frigorino.Domain.Entities;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class ListItemQuantityPersistenceTests
    {
        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task ListItem_RoundTripsStructuredQuantity()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "milk",
                    QuantityValue = 1.5m,
                    QuantityUnit = QuantityUnit.Liter,
                });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal(1.5m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Liter, item.QuantityUnit);
        }

        [Fact]
        public async Task ListItem_RoundTripsNullQuantity()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem { ListId = 1, Text = "call dentist" });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }
    }
}
