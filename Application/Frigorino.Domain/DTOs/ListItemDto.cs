namespace Frigorino.Domain.DTOs
{
    public class ListItemDto
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public bool Status { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateListItemRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
    }

    public class UpdateListItemRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public bool Status { get; set; }
    }

    public class ReorderItemRequest
    {
        public int AfterItemId { get; set; } // 0 means move to top of section
    }

    public class ToggleStatusRequest
    {
        public bool Status { get; set; }
    }
}
