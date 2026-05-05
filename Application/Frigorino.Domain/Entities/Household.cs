using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class Household
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public User CreatedByUser { get; set; } = null!;
        public ICollection<UserHousehold> UserHouseholds { get; set; } = new List<UserHousehold>();
        public ICollection<List> Lists { get; set; } = new List<List>();
        public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public static Result<Household> Create(string name, string? description, string ownerUserId)
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Household name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                errors.Add(new Error("Owner user id is required.")
                    .WithMetadata("Property", nameof(CreatedByUserId)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<Household>(errors);
            }

            var now = DateTime.UtcNow;
            var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            var household = new Household
            {
                Name = name.Trim(),
                Description = trimmedDescription,
                CreatedByUserId = ownerUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            // Seed owner membership in the navigation collection so a single SaveChanges persists
            // both rows in one transaction (EF relationship fixup assigns HouseholdId on insert).
            // ApplicationDbContext.SaveChangesAsync only re-stamps timestamps when == default,
            // so the values set here are preserved.
            household.UserHouseholds.Add(new UserHousehold
            {
                UserId = ownerUserId,
                Role = HouseholdRole.Owner,
                JoinedAt = now,
                IsActive = true,
            });
            return Result.Ok(household);
        }
    }
}
