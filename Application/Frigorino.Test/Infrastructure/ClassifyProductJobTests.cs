using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class ClassifyProductJobTests
    {
        private const int HouseholdId = 42;

        // Deterministic classifier: returns a fixed result, records the calls it received.
        private sealed class FakeClassifier : IItemClassifier
        {
            private readonly Result<ProductClassification> _result;
            public int Version { get; }
            public int Calls { get; private set; }

            public FakeClassifier(Result<ProductClassification> result, int version)
            {
                _result = result;
                Version = version;
            }

            public Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(_result);
            }
        }

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static Result<ProductClassification> AiResult(int days) =>
            Result.Ok(new ProductClassification(
                ProductCategory.DairyAndEggs, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value));

        [Fact]
        public async Task Run_NewName_ClassifiesAndInserts()
        {
            var dbName = Guid.NewGuid().ToString();
            var classifier = new FakeClassifier(AiResult(7), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "  Milk  ", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var product = await verify.Products.SingleAsync();
            Assert.Equal("milk", product.NormalizedName);
            Assert.Equal(ProductCategory.DairyAndEggs, product.ClassificationProductCategory);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.ClassificationExpiryHandling);
            Assert.Equal(7, product.ClassificationShelfLifeDays);
            Assert.Equal(1, product.ClassifierVersion);
            Assert.Equal(1, classifier.Calls);
        }

        [Fact]
        public async Task Run_AlreadyClassifiedAtCurrentVersion_SkipsClassifier()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var seed = NewContext(dbName))
            {
                seed.Products.Add(Product.Create(HouseholdId, "milk", AiResult(7).Value, 1).Value);
                await seed.SaveChangesAsync();
            }

            var classifier = new FakeClassifier(AiResult(7), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "milk", CancellationToken.None);
            }

            Assert.Equal(0, classifier.Calls);
        }

        [Fact]
        public async Task Run_StaleVersion_ReclassifiesAndUpdates()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var seed = NewContext(dbName))
            {
                seed.Products.Add(Product.Create(HouseholdId, "milk", AiResult(7).Value, 1).Value);
                await seed.SaveChangesAsync();
            }

            var classifier = new FakeClassifier(
                Result.Ok(new ProductClassification(ProductCategory.Other, ExpiryProfile.NonPerishable)), version: 2);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "milk", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var product = await verify.Products.SingleAsync();
            Assert.Equal(ProductCategory.Other, product.ClassificationProductCategory);
            Assert.Equal(ExpiryHandling.NonPerishable, product.ClassificationExpiryHandling);
            Assert.Equal(2, product.ClassifierVersion);
            Assert.Equal(1, classifier.Calls);
        }

        [Fact]
        public async Task Run_ClassifierFails_WritesNothing()
        {
            var dbName = Guid.NewGuid().ToString();
            var classifier = new FakeClassifier(
                Result.Fail<ProductClassification>("transient"), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "milk", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            Assert.Equal(0, await verify.Products.CountAsync());
        }

        [Fact]
        public async Task Run_EmptyName_NoOp()
        {
            var dbName = Guid.NewGuid().ToString();
            var classifier = new FakeClassifier(AiResult(7), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "   ", CancellationToken.None);
            }

            Assert.Equal(0, classifier.Calls);
            using var verify = NewContext(dbName);
            Assert.Equal(0, await verify.Products.CountAsync());
        }
    }
}
