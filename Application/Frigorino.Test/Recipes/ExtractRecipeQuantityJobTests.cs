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
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class ExtractRecipeQuantityJobTests
    {
        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task Run_AppliesQuantity_AndNeverClassifies()
        {
            var dbName = Guid.NewGuid().ToString();
            int recipeId;
            int itemId;

            // Seed recipe + item in their own context scope.
            using (var seed = NewContext(dbName))
            {
                var recipe = Recipe.Create("Pie", null, 1, "u1").Value;
                var section = recipe.AddSection(null, null).Value;
                seed.Recipes.Add(recipe);
                await seed.SaveChangesAsync();

                var item = recipe.AddItem(section.Id, "250g flour", null, null).Value;
                await seed.SaveChangesAsync();

                recipeId = recipe.Id;
                itemId = item.Id;
            }

            var extractor = A.Fake<IQuantityExtractor>();
            A.CallTo(() => extractor.ExtractAsync("250g flour", A<CancellationToken>._))
                .Returns(Result.Ok(new QuantityExtraction("Flour", Quantity.Create(250, QuantityUnit.Gram).Value)));

            // classificationTrigger is NOT passed to the job — it has no such dependency.
            // We create a fake merely to assert MustNotHaveHappened (by construction it can't).
            var classificationTrigger = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractRecipeQuantityJob(db, extractor, NullLogger<ExtractRecipeQuantityJob>.Instance);
                await job.Run(1, recipeId, itemId, "250g flour", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var saved = await verify.RecipeItems.SingleAsync(i => i.Id == itemId);
            Assert.Equal("Flour", saved.Text);
            Assert.Equal(250m, saved.QuantityValue);
            // Real end-to-end guard for the MVP "recipes never classify" decision: the classification
            // chain (OnProductReferenced) is what creates Product rows, so the recipe extraction path
            // must leave the Products table empty. (The MustNotHaveHappened below is vacuous — the fake
            // is never wired into the job — so this is the load-bearing assertion.)
            Assert.Empty(verify.Products);
            // The job has NO IProductClassificationTrigger dependency at all — kept for documentation:
            A.CallTo(classificationTrigger).MustNotHaveHappened();
        }
    }
}
