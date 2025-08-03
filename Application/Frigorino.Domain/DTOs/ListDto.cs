using Frigorino.Domain.Entities;

namespace Frigorino.Domain.DTOs
{
    public class ListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int HouseholdId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserDto CreatedByUser { get; set; } = null!;
    }

    public class CreateListRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateListRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
