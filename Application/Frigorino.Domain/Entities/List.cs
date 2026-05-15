using FluentResults;
using Frigorino.Domain.Errors;

namespace Frigorino.Domain.Entities
{
    public class List
    {
        // Source of truth for length constraints. Both the factory (List.Create) and the
        // EF configuration (ListConfiguration) read from these so DB and aggregate agree.
        public const int NameMaxLength = 255;
        public const int DescriptionMaxLength = 1000;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int HouseholdId { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Household Household { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public ICollection<ListItem> ListItems { get; set; } = new System.Collections.Generic.List<ListItem>();

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public static Result<List> Create(string name, string? description, int householdId, string createdByUserId)
        {
            var errors = new System.Collections.Generic.List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("List name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"List name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"List description must be {DescriptionMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Description)));
            }
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.")
                    .WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(createdByUserId))
            {
                errors.Add(new Error("Creator user id is required.")
                    .WithMetadata("Property", nameof(CreatedByUserId)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<List>(errors);
            }

            var now = DateTime.UtcNow;
            var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            var list = new List
            {
                Name = name.Trim(),
                Description = trimmedDescription,
                HouseholdId = householdId,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            return Result.Ok(list);
        }

        // Aggregate-internal mutation. Edit permission is creator-OR-Admin+ — the legacy rule
        // preserved from ListService. Caller's role is passed in because it lives on
        // UserHousehold (a different aggregate) and the handler resolves it once.
        public Result Update(string callerUserId, HouseholdRole callerRole, string name, string? description)
        {
            if (CreatedByUserId != callerUserId && callerRole < HouseholdRole.Admin)
            {
                return Result.Fail(
                    new AccessDeniedError("Only the list creator or an admin can edit this list."));
            }

            var errors = new System.Collections.Generic.List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("List name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"List name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"List description must be {DescriptionMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Description)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail(errors);
            }

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // Aggregate-internal mutation. Same role policy as Update — creator OR Admin+.
        public Result SoftDelete(string callerUserId, HouseholdRole callerRole)
        {
            if (CreatedByUserId != callerUserId && callerRole < HouseholdRole.Admin)
            {
                return Result.Fail(
                    new AccessDeniedError("Only the list creator or an admin can delete this list."));
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }
    }
}
