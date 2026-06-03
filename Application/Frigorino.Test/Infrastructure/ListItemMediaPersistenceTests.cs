using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class ListItemMediaPersistenceTests
    {
        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task ListItem_RoundTripsMediaColumns()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "",
                    Type = ListItemType.Image,
                    StorageKey = "images/key-1",
                    ThumbnailStorageKey = "images/thumb-1",
                    OriginalFileName = "fridge.jpg",
                    ContentType = "image/jpeg",
                    FileSizeBytes = 1024,
                });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Image, item.Type);
            Assert.Equal("images/key-1", item.StorageKey);
            Assert.Equal("images/thumb-1", item.ThumbnailStorageKey);
            Assert.Equal("fridge.jpg", item.OriginalFileName);
            Assert.Equal("image/jpeg", item.ContentType);
            Assert.Equal(1024, item.FileSizeBytes);
        }

        [Fact]
        public async Task ListItem_DefaultsToTextType()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem { ListId = 1, Text = "milk" });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Text, item.Type);
            Assert.Null(item.StorageKey);
        }
    }
}
