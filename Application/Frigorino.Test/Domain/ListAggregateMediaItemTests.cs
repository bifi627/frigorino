using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for List.AddMediaItem and the uniform behavior of media items through the
    // existing item lifecycle methods. No DbContext.
    public class ListAggregateMediaItemTests
    {
        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        private static List NewList()
        {
            return new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = HouseholdId,
                CreatedByUserId = CreatorId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true,
            };
        }

        private static StoredFile ImageFile() =>
            new("images/key-1", "images/thumb-1", "image/jpeg", "fridge.jpg", 1024);

        private static StoredFile DocumentFile() =>
            new("docs/key-1", null, "application/pdf", "warranty.pdf", 2048);

        // ------- happy paths -------

        [Fact]
        public void AddMediaItem_ValidImage_SetsColumnsAndPlacesInUnchecked()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Image, "front of fridge", ImageFile());

            Assert.True(result.IsSuccess);
            var item = result.Value;
            Assert.Equal(ListItemType.Image, item.Type);
            Assert.Equal("", item.Text);
            Assert.Equal("front of fridge", item.Comment);
            Assert.Equal("images/key-1", item.StorageKey);
            Assert.Equal("images/thumb-1", item.ThumbnailStorageKey);
            Assert.Equal("image/jpeg", item.ContentType);
            Assert.Equal("fridge.jpg", item.OriginalFileName);
            Assert.Equal(1024, item.FileSizeBytes);
            Assert.False(item.Status);
            Assert.True(item.IsActive);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item.SortOrder);
        }

        [Fact]
        public void AddMediaItem_ValidDocument_HasNoThumbnail()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Document, null, DocumentFile());

            Assert.True(result.IsSuccess);
            Assert.Equal(ListItemType.Document, result.Value.Type);
            Assert.Null(result.Value.ThumbnailStorageKey);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddMediaItem_TrimsCaptionIntoComment()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Image, "  hello  ", ImageFile());

            Assert.True(result.IsSuccess);
            Assert.Equal("hello", result.Value.Comment);
        }

        // ------- rejections -------

        [Fact]
        public void AddMediaItem_TextType_Fails()
        {
            var list = NewList();

            var result = list.AddMediaItem(ListItemType.Text, null, ImageFile());

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Type), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_DisallowedContentTypeForImage_FailsKeyedOnContentType()
        {
            var list = NewList();
            var badType = new StoredFile("k", "t", "application/pdf", "x.pdf", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, badType);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ContentType), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_DisallowedContentTypeForDocument_FailsKeyedOnContentType()
        {
            var list = NewList();
            var badType = new StoredFile("k", null, "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Document, null, badType);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ContentType), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_ZeroSize_FailsKeyedOnFileSize()
        {
            var list = NewList();
            var zero = new StoredFile("k", "t", "image/jpeg", "x.jpg", 0);

            var result = list.AddMediaItem(ListItemType.Image, null, zero);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.FileSizeBytes), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_OverSizeCap_FailsKeyedOnFileSize()
        {
            var list = NewList();
            var tooBig = new StoredFile("k", "t", "image/jpeg", "x.jpg", ListItem.MaxFileSizeBytes + 1);

            var result = list.AddMediaItem(ListItemType.Image, null, tooBig);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.FileSizeBytes), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_MissingStorageKey_FailsKeyedOnStorageKey()
        {
            var list = NewList();
            var noKey = new StoredFile("  ", "t", "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, noKey);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.StorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_StorageKeyTooLong_FailsKeyedOnStorageKey()
        {
            var list = NewList();
            var tooLong = new StoredFile(new string('k', ListItem.StorageKeyMaxLength + 1), "t", "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.StorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_FileNameTooLong_FailsKeyedOnFileName()
        {
            var list = NewList();
            var tooLong = new StoredFile("k", "t", "image/jpeg", new string('x', ListItem.OriginalFileNameMaxLength + 1), 10);

            var result = list.AddMediaItem(ListItemType.Image, null, tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.OriginalFileName), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_MissingFileName_FailsKeyedOnFileName()
        {
            var list = NewList();
            var noName = new StoredFile("k", "t", "image/jpeg", "   ", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, noName);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.OriginalFileName), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_ImageWithoutThumbnail_FailsKeyedOnThumbnail()
        {
            var list = NewList();
            var noThumb = new StoredFile("k", null, "image/jpeg", "x.jpg", 10);

            var result = list.AddMediaItem(ListItemType.Image, null, noThumb);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ThumbnailStorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_DocumentWithThumbnail_FailsKeyedOnThumbnail()
        {
            var list = NewList();
            var withThumb = new StoredFile("k", "t", "application/pdf", "x.pdf", 10);

            var result = list.AddMediaItem(ListItemType.Document, null, withThumb);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.ThumbnailStorageKey), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddMediaItem_CaptionTooLong_FailsKeyedOnComment()
        {
            var list = NewList();
            var caption = new string('x', ListItem.CommentMaxLength + 1);

            var result = list.AddMediaItem(ListItemType.Image, caption, ImageFile());

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Comment), result.Errors[0].Metadata["Property"]);
        }

        // ------- lifecycle uniformity -------

        [Fact]
        public void MediaItem_TogglesReordersCompacts_LikeTextItems()
        {
            var list = NewList();
            var media = list.AddMediaItem(ListItemType.Image, null, ImageFile()).Value;
            list.AddItem("Milk");

            Assert.True(list.ToggleItemStatus(media.Id).IsSuccess);
            Assert.True(media.Status);
            Assert.True(list.CompactItems().IsSuccess);
            Assert.True(list.ReorderItem(media.Id, 0).IsSuccess);
        }

        [Fact]
        public void MediaItem_SoftDeleteRetainsFileColumns_AndRestores()
        {
            var list = NewList();
            var media = list.AddMediaItem(ListItemType.Image, "cap", ImageFile()).Value;

            Assert.True(list.RemoveItem(media.Id).IsSuccess);
            Assert.False(media.IsActive);
            // Blob metadata must survive soft-delete so restore re-exposes the same file.
            Assert.Equal("images/key-1", media.StorageKey);
            Assert.Equal("images/thumb-1", media.ThumbnailStorageKey);

            var restored = list.RestoreItem(media.Id);
            Assert.True(restored.IsSuccess);
            Assert.True(media.IsActive);
            Assert.Equal("images/key-1", media.StorageKey);
        }
    }
}
