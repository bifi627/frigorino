using System.Text;
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Lists.Items;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class GetItemFileSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        private static async Task<(int listId, int itemId)> SeedImageItemAsync(TestApplicationDbContext db, string userId)
        {
            db.Households.Add(new Household { Id = 1, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId, HouseholdId = 1, Role = HouseholdRole.Member,
                IsActive = true, JoinedAt = DateTime.UtcNow,
            });
            var list = new List { Name = "L", HouseholdId = 1, CreatedByUserId = userId, IsActive = true };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            var item = new ListItem
            {
                ListId = list.Id, Type = ListItemType.Image, Text = "",
                StorageKey = "full-key", ThumbnailStorageKey = "thumb-key",
                ContentType = "image/webp", OriginalFileName = "p.jpg", FileSizeBytes = 3, IsActive = true,
            };
            db.ListItems.Add(item);
            await db.SaveChangesAsync();
            return (list.Id, item.Id);
        }

        [Fact]
        public async Task GetFile_Member_StreamsBytesWithContentType()
        {
            using var db = NewContext();
            var (listId, itemId) = await SeedImageItemAsync(db, "u1");
            var storage = A.Fake<IFileStorage>();
            A.CallTo(() => storage.OpenAsync("full-key", A<CancellationToken>._))
                .Returns<Stream?>(new MemoryStream(Encoding.UTF8.GetBytes("img")));

            var result = await GetItemFileEndpoint.Handle(
                1, listId, itemId, UserNamed("u1"), db, storage, new DefaultHttpContext(), CancellationToken.None);

            var file = Assert.IsType<FileStreamHttpResult>(result.Result);
            Assert.Equal("image/webp", file.ContentType);
        }

        [Fact]
        public async Task GetFile_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var (listId, itemId) = await SeedImageItemAsync(db, "u1");
            var storage = A.Fake<IFileStorage>();

            var result = await GetItemFileEndpoint.Handle(
                1, listId, itemId, UserNamed("intruder"), db, storage, new DefaultHttpContext(), CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task GetThumbnail_MissingBlob_ReturnsNotFound()
        {
            using var db = NewContext();
            var (listId, itemId) = await SeedImageItemAsync(db, "u1");
            var storage = A.Fake<IFileStorage>();
            A.CallTo(() => storage.OpenAsync("thumb-key", A<CancellationToken>._)).Returns<Stream?>(null);

            var result = await GetItemThumbnailEndpoint.Handle(
                1, listId, itemId, UserNamed("u1"), db, storage, new DefaultHttpContext(), CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }
    }
}
