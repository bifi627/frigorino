using Frigorino.Domain.Entities;
using Frigorino.Features.Lists.Items;

namespace Frigorino.Test.Features
{
    public class ListItemResponseProjectionTests
    {
        [Fact]
        public void From_MediaItem_MapsMediaFields()
        {
            var item = new ListItem
            {
                Id = 5,
                ListId = 2,
                Type = ListItemType.Image,
                Text = "",
                Comment = "the blue one",
                StorageKey = "abc",
                ThumbnailStorageKey = "def",
                OriginalFileName = "photo.jpg",
                ContentType = "image/webp",
                FileSizeBytes = 1234,
                Status = false,
                SortOrder = 1000,
            };

            var dto = ListItemResponse.From(item);

            Assert.Equal(ListItemType.Image, dto.Type);
            Assert.Equal("photo.jpg", dto.FileName);
            Assert.Equal("image/webp", dto.ContentType);
            Assert.Equal(1234, dto.FileSize);
            Assert.Equal("the blue one", dto.Comment);
        }

        [Fact]
        public void From_TextItem_LeavesMediaFieldsNull()
        {
            var item = new ListItem { Id = 1, ListId = 1, Type = ListItemType.Text, Text = "Milk" };

            var dto = ListItemResponse.From(item);

            Assert.Equal(ListItemType.Text, dto.Type);
            Assert.Null(dto.FileName);
            Assert.Null(dto.ContentType);
            Assert.Null(dto.FileSize);
        }
    }
}
