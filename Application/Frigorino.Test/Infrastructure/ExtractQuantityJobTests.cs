using FakeItEasy;
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class ExtractQuantityJobTests
    {
        private const int HouseholdId = 42;
        private const int ListId = 7;
        private const int ItemId = 100;

        private sealed class FakeExtractor : IQuantityExtractor
        {
            private readonly Result<QuantityExtraction> _result;
            public int Calls { get; private set; }
            public FakeExtractor(Result<QuantityExtraction> result) => _result = result;
            public Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)
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

        private static async Task SeedListWithItem(string dbName, string text)
        {
            using var db = NewContext(dbName);
            db.Lists.Add(new List
            {
                Id = ListId,
                HouseholdId = HouseholdId,
                Name = "Groceries",
                CreatedByUserId = "u",
                IsActive = true,
                ListItems = { new ListItem { Id = ItemId, ListId = ListId, Text = text, IsActive = true } },
            });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Run_WithQuantity_WritesBackAndTriggersClassificationOnCleanName()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedListWithItem(dbName, "20 apples");
            var extractor = new FakeExtractor(Result.Ok(
                new QuantityExtraction("apples", Quantity.Create(20, QuantityUnit.Piece).Value)));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "20 apples", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal("apples", item.Text);
            Assert.Equal(20m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Piece, item.QuantityUnit);
            A.CallTo(() => classification.OnProductReferenced(HouseholdId, "apples")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Run_NoQuantity_RewritesTextTriggersClassification()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedListWithItem(dbName, "7 up");
            var extractor = new FakeExtractor(Result.Ok(new QuantityExtraction("7up", null)));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "7 up", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal("7up", item.Text);
            Assert.Null(item.QuantityValue);
            A.CallTo(() => classification.OnProductReferenced(HouseholdId, "7up")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Run_ExtractorFails_WritesNothingAndDoesNotClassify()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedListWithItem(dbName, "20 apples");
            var extractor = new FakeExtractor(Result.Fail<QuantityExtraction>("transient"));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "20 apples", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal("20 apples", item.Text);
            Assert.Null(item.QuantityValue);
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Run_ItemMissing_NoOp()
        {
            var dbName = Guid.NewGuid().ToString();
            var extractor = new FakeExtractor(Result.Ok(new QuantityExtraction("x", null)));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "x", CancellationToken.None);
            }

            Assert.Equal(0, extractor.Calls);
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
