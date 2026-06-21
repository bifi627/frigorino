using System.Text;
using FakeItEasy;
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Lists.Items;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Features
{
    public class CreateMediaItemSliceTests
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

        private static async Task<int> SeedListAsync(TestApplicationDbContext db, string userId, int householdId)
        {
            db.Households.Add(new Household { Id = householdId, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId, HouseholdId = householdId, Role = HouseholdRole.Member,
                IsActive = true, JoinedAt = DateTime.UtcNow,
            });
            var list = new List
            {
                Name = "Groceries", HouseholdId = householdId, CreatedByUserId = userId,
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            return list.Id;
        }

        private static IFormFile FakeFile(string name, long length, byte[]? content = null, string contentType = "image/jpeg")
        {
            var stream = new MemoryStream(content ?? Encoding.UTF8.GetBytes("raw-bytes"));
            return new FormFile(stream, 0, length, "file", name)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType,
            };
        }

        private static IFileStorage SingleKeyStorage(string key, out IFileStorage storage)
        {
            var fake = A.Fake<IFileStorage>();
            A.CallTo(() => fake.SaveAsync(A<Stream>._, A<CancellationToken>._)).Returns(key);
            storage = fake;
            return fake;
        }

        private static IImageProcessor OkProcessor() =>
            FakeProcessor(Result.Ok(new ProcessedImage(
                new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, "image/webp", 3)));

        private static IImageProcessor FakeProcessor(Result<ProcessedImage> result)
        {
            var p = A.Fake<IImageProcessor>();
            A.CallTo(() => p.ProcessAsync(A<Stream>._, A<CancellationToken>._)).Returns(result);
            return p;
        }

        private static IFileStorage SequentialStorage(out IFileStorage storage)
        {
            var fake = A.Fake<IFileStorage>();
            A.CallTo(() => fake.SaveAsync(A<Stream>._, A<CancellationToken>._))
                .ReturnsNextFromSequence("key-full", "key-thumb");
            storage = fake;
            return fake;
        }

        [Fact]
        public async Task Post_ValidImage_PersistsMediaItemAndSavesBothBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("photo.jpg", length: 2048),
                ListItemType.Image, caption: "the blue one",
                UserNamed("u1"), db, storage, OkProcessor(),
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<Created<ListItemResponse>>(result.Result);

            var row = await db.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Image, row.Type);
            Assert.Equal("key-full", row.StorageKey);
            Assert.Equal("key-thumb", row.ThumbnailStorageKey);
            Assert.Equal("image/webp", row.ContentType);
            Assert.Equal("photo.jpg", row.OriginalFileName);
            Assert.Equal("the blue one", row.Comment);
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._))
                .MustHaveHappenedTwiceExactly();
            A.CallTo(() => storage.DeleteAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, null,
                UserNamed("intruder"), db, storage, OkProcessor(),
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_OverSizeCap_Returns413_WithoutProcessing()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);
            var processor = OkProcessor();

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("big.jpg", length: ListItem.MaxFileSizeBytes + 1),
                ListItemType.Image, null,
                UserNamed("u1"), db, storage, processor,
                NullLoggerFactory.Instance, CancellationToken.None);

            var status = Assert.IsType<StatusCodeHttpResult>(result.Result);
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
            A.CallTo(() => processor.ProcessAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_UndecodableImage_Returns400_AndSavesNoBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);
            var processor = FakeProcessor(Result.Fail("bad image"));

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, null,
                UserNamed("u1"), db, storage, processor,
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_AggregateRejects_CompensatesByDeletingBothBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SequentialStorage(out var storage);

            // Caption longer than CommentMaxLength → AddMediaItem fails AFTER blobs are saved.
            var tooLong = new string('x', ListItem.CommentMaxLength + 1);

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, tooLong,
                UserNamed("u1"), db, storage, OkProcessor(),
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
            Assert.Empty(await db.ListItems.ToListAsync());
            A.CallTo(() => storage.DeleteAsync("key-full", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => storage.DeleteAsync("key-thumb", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Post_SecondBlobSaveThrows_CompensatesFirstBlob()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);

            // First save succeeds (full-res), second save (thumbnail) throws → the first blob must be compensated.
            var storage = A.Fake<IFileStorage>();
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._))
                .Returns("key-full").Once()
                .Then
                .Throws<IOException>();

            await Assert.ThrowsAsync<IOException>(() => CreateMediaItemEndpoint.Handle(
                householdId: 1, listId, FakeFile("p.jpg", 10), ListItemType.Image, null,
                UserNamed("u1"), db, storage, OkProcessor(),
                NullLoggerFactory.Instance, CancellationToken.None));

            Assert.Empty(await db.ListItems.ToListAsync());
            A.CallTo(() => storage.DeleteAsync("key-full", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Post_ValidDocument_PersistsDocumentItem_OneBlob_NoThumbnail_NoProcessing()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SingleKeyStorage("key-doc", out var storage);
            var processor = OkProcessor();

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("manual.pdf", length: 4096, contentType: "application/pdf"),
                ListItemType.Document, caption: "warranty",
                UserNamed("u1"), db, storage, processor,
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<Created<ListItemResponse>>(result.Result);

            var row = await db.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Document, row.Type);
            Assert.Equal("key-doc", row.StorageKey);
            Assert.Null(row.ThumbnailStorageKey);
            Assert.Equal("application/pdf", row.ContentType);
            Assert.Equal("manual.pdf", row.OriginalFileName);
            Assert.Equal(4096, row.FileSizeBytes);
            Assert.Equal("warranty", row.Comment);

            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => processor.ProcessAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_DocumentWithDisallowedContentType_Returns400_SavesNoBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SingleKeyStorage("key-doc", out var storage);
            var processor = OkProcessor();

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("archive.zip", length: 4096, contentType: "application/zip"),
                ListItemType.Document, caption: null,
                UserNamed("u1"), db, storage, processor,
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
            Assert.Empty(await db.ListItems.ToListAsync());
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => processor.ProcessAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
    }
}
