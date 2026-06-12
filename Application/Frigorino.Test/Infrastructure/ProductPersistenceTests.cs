using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class ProductPersistenceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task SaveChanges_StampsTimestampsOnNewProduct()
        {
            using var db = NewContext();
            var classification = new ProductClassification(
                ProductCategory.DairyAndEggs, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value);
            var product = Product.Create(42, "milk", classification, 1).Value;

            db.Products.Add(product);
            await db.SaveChangesAsync();

            Assert.NotEqual(default, product.CreatedAt);
            Assert.NotEqual(default, product.UpdatedAt);
        }
    }
}
