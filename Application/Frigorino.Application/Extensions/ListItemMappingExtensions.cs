using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Application.Extensions
{
    public static class ListItemMappingExtensions
    {
        public static ListItemDto ToDto(this ListItem listItem)
        {
            return new ListItemDto
            {
                Id = listItem.Id,
                ListId = listItem.ListId,
                Text = listItem.Text,
                Quantity = listItem.Quantity,
                Status = listItem.Status,
                SortOrder = listItem.SortOrder,
                CreatedAt = listItem.CreatedAt,
                UpdatedAt = listItem.UpdatedAt
            };
        }

        public static IEnumerable<ListItemDto> ToDto(this IEnumerable<ListItem> listItems)
        {
            return listItems.Select(li => li.ToDto());
        }

        public static ListItem ToEntity(this CreateListItemRequest request, int listId, int sortOrder)
        {
            return new ListItem
            {
                ListId = listId,
                Text = request.Text,
                Quantity = request.Quantity,
                Status = false, // New items are always unchecked
                SortOrder = sortOrder
            };
        }

        public static void UpdateFromRequest(this ListItem listItem, UpdateListItemRequest request)
        {
            listItem.Text = request.Text ?? listItem.Text;
            listItem.Quantity = request.Quantity ?? listItem.Quantity;
            listItem.Status = request.Status ?? listItem.Status;
        }
    }
}
