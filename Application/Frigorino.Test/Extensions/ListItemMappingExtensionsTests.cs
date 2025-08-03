using Frigorino.Application.Extensions;
using Frigorino.Application.Utilities;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Test.Extensions
{
    public class ListItemMappingExtensionsTests
    {
        [Fact]
        public void ToDto_ShouldMapAllProperties()
        {
            // Arrange
            var listItem = new ListItem
            {
                Id = 1,
                ListId = 5,
                Text = "Test item",
                Quantity = "2 kg",
                Status = true,
                SortOrder = 101_000,
                CreatedAt = new DateTime(2025, 8, 3, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 8, 3, 11, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };

            // Act
            var dto = listItem.ToDto();

            // Assert
            Assert.Equal(listItem.Id, dto.Id);
            Assert.Equal(listItem.ListId, dto.ListId);
            Assert.Equal(listItem.Text, dto.Text);
            Assert.Equal(listItem.Quantity, dto.Quantity);
            Assert.Equal(listItem.Status, dto.Status);
            Assert.Equal(listItem.SortOrder, dto.SortOrder);
            Assert.Equal(listItem.CreatedAt, dto.CreatedAt);
            Assert.Equal(listItem.UpdatedAt, dto.UpdatedAt);
        }

        [Fact]
        public void ToDto_Collection_ShouldMapAllItems()
        {
            // Arrange
            var listItems = new List<ListItem>
            {
                new ListItem { Id = 1, Text = "Item 1", SortOrder = 100_000 },
                new ListItem { Id = 2, Text = "Item 2", SortOrder = 101_000 },
                new ListItem { Id = 3, Text = "Item 3", SortOrder = 1_100_000, Status = true }
            };

            // Act
            var dtos = listItems.ToDto().ToList();

            // Assert
            Assert.Equal(3, dtos.Count);
            Assert.Equal("Item 1", dtos[0].Text);
            Assert.Equal("Item 2", dtos[1].Text);
            Assert.Equal("Item 3", dtos[2].Text);
            Assert.False(dtos[0].Status);
            Assert.False(dtos[1].Status);
            Assert.True(dtos[2].Status);
        }

        [Fact]
        public void ToEntity_ShouldCreateCorrectEntity()
        {
            // Arrange
            var request = new CreateListItemRequest
            {
                Text = "New item",
                Quantity = "5 pieces"
            };
            var listId = 10;
            var sortOrder = SortOrderCalculator.GetNewItemSortOrder();

            // Act
            var entity = request.ToEntity(listId, sortOrder);

            // Assert
            Assert.Equal(listId, entity.ListId);
            Assert.Equal(request.Text, entity.Text);
            Assert.Equal(request.Quantity, entity.Quantity);
            Assert.False(entity.Status); // New items should always be unchecked
            Assert.Equal(sortOrder, entity.SortOrder);
            Assert.True(entity.IsActive); // Should default to true
        }

        [Fact]
        public void ToEntity_WithNullQuantity_ShouldHandleGracefully()
        {
            // Arrange
            var request = new CreateListItemRequest
            {
                Text = "Item without quantity",
                Quantity = null
            };
            var listId = 15;
            var sortOrder = 150_000;

            // Act
            var entity = request.ToEntity(listId, sortOrder);

            // Assert
            Assert.Equal(listId, entity.ListId);
            Assert.Equal(request.Text, entity.Text);
            Assert.Null(entity.Quantity);
            Assert.False(entity.Status);
            Assert.Equal(sortOrder, entity.SortOrder);
        }

        [Fact]
        public void UpdateFromRequest_ShouldUpdateAllFields()
        {
            // Arrange
            var listItem = new ListItem
            {
                Id = 1,
                Text = "Original text",
                Quantity = "Original quantity",
                Status = false,
                SortOrder = 100_000
            };

            var updateRequest = new UpdateListItemRequest
            {
                Text = "Updated text",
                Quantity = "Updated quantity",
                Status = true
            };

            // Act
            listItem.UpdateFromRequest(updateRequest);

            // Assert
            Assert.Equal(updateRequest.Text, listItem.Text);
            Assert.Equal(updateRequest.Quantity, listItem.Quantity);
            Assert.Equal(updateRequest.Status, listItem.Status);
            Assert.Equal(100_000, listItem.SortOrder); // SortOrder should not be changed by this method
            Assert.Equal(1, listItem.Id); // Id should remain unchanged
        }

        [Fact]
        public void UpdateFromRequest_WithNullQuantity_ShouldSetToNull()
        {
            // Arrange
            var listItem = new ListItem
            {
                Text = "Original text",
                Quantity = "Some quantity",
                Status = false
            };

            var updateRequest = new UpdateListItemRequest
            {
                Text = "Updated text",
                Quantity = null,
                Status = true
            };

            // Act
            listItem.UpdateFromRequest(updateRequest);

            // Assert
            Assert.Equal("Updated text", listItem.Text);
            Assert.Null(listItem.Quantity);
            Assert.True(listItem.Status);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("Short", "Short")]
        [InlineData("Very long item text that might be used in real applications", "Very long item text that might be used in real applications")]
        public void ToEntity_VariousTextLengths_ShouldPreserveText(string inputText, string expectedText)
        {
            // Arrange
            var request = new CreateListItemRequest
            {
                Text = inputText,
                Quantity = "1"
            };

            // Act
            var entity = request.ToEntity(1, 100_000);

            // Assert
            Assert.Equal(expectedText, entity.Text);
        }
    }
}
