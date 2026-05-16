using FluentResults;
using Frigorino.Domain.Errors;

namespace Frigorino.Domain.Entities
{
    public class Inventory
    {
        // Source of truth for length constraints. Both the factory (Inventory.Create) and the
        // EF configuration (InventoryConfiguration) read from these so DB and aggregate agree.
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
        public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public static Result<Inventory> Create(string name, string? description, int householdId, string createdByUserId)
        {
            var errors = new System.Collections.Generic.List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Inventory name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Inventory name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"Inventory description must be {DescriptionMaxLength} characters or fewer.")
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
                return Result.Fail<Inventory>(errors);
            }

            var now = DateTime.UtcNow;
            var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            var inventory = new Inventory
            {
                Name = name.Trim(),
                Description = trimmedDescription,
                HouseholdId = householdId,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            return Result.Ok(inventory);
        }

        // Aggregate-internal mutation. Edit permission is creator-OR-Admin+ — the legacy rule
        // preserved from InventoryService. Caller's role is passed in because it lives on
        // UserHousehold (a different aggregate) and the handler resolves it once.
        public Result Update(string callerUserId, HouseholdRole callerRole, string name, string? description)
        {
            if (CreatedByUserId != callerUserId && callerRole < HouseholdRole.Admin)
            {
                return Result.Fail(
                    new AccessDeniedError("Only the inventory creator or an admin can edit this inventory."));
            }

            var errors = new System.Collections.Generic.List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Inventory name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Inventory name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"Inventory description must be {DescriptionMaxLength} characters or fewer.")
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
                    new AccessDeniedError("Only the inventory creator or an admin can delete this inventory."));
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // ------- InventoryItem coordination -------
        //
        // Item-level operations live on Inventory because sort-order is a multi-row invariant of
        // the aggregate (single section in the unchecked range 1M..9M — items have no status flag,
        // unlike ListItem). Items intentionally have no role gate: any active household member can
        // add / update / reorder / delete / compact, matching the legacy InventoryItemService
        // behaviour. The handler enforces membership; the aggregate doesn't take callerRole.

        public Result<InventoryItem> AddItem(string text, string? quantity, DateTime? expiryDate)
        {
            var errors = ValidateItemFields(text, quantity, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<InventoryItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new InventoryItem
            {
                InventoryId = Id,
                Text = text.Trim(),
                Quantity = NormaliseQuantity(quantity),
                ExpiryDate = expiryDate,
                SortOrder = ComputeAppendSortOrder(),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            InventoryItems.Add(item);
            return Result.Ok(item);
        }

        // Text/Quantity preserve on null (caller intent: "leave existing value"). ExpiryDate is
        // intentionally write-through — null means "clear the value", because ExpiryDate is a
        // first-class field the user explicitly sets/unsets via a date picker, and the legacy
        // mapping extension assigned it unconditionally. Comments out the asymmetry deliberately.
        public Result<InventoryItem> UpdateItem(int itemId, string? text, string? quantity, DateTime? expiryDate)
        {
            var item = InventoryItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<InventoryItem>(
                    new EntityNotFoundError($"Inventory item {itemId} not found."));
            }

            // Text is required only when the caller actually supplies a value — null means
            // "preserve existing". Empty/whitespace, on the other hand, is a real attempt at
            // a blank text and is rejected.
            var errors = ValidateItemFields(text, quantity, requireText: text is not null);
            if (errors.Count > 0)
            {
                return Result.Fail<InventoryItem>(errors);
            }

            if (text is not null)
            {
                item.Text = text.Trim();
            }
            if (quantity is not null)
            {
                item.Quantity = NormaliseQuantity(quantity);
            }
            item.ExpiryDate = expiryDate;

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result RemoveItem(int itemId)
        {
            var item = InventoryItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail(
                    new EntityNotFoundError($"Inventory item {itemId} not found."));
            }

            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // AfterItemId == 0 means "move to the top of the section". An afterItemId that doesn't
        // resolve to an active sibling silently falls back to top-of-section — preserves the
        // legacy InventoryItemService wire contract the frontend's optimistic UI depends on.
        // Self-anchor (afterItemId == itemId) is a no-op.
        public Result<InventoryItem> ReorderItem(int itemId, int afterItemId)
        {
            var item = InventoryItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<InventoryItem>(
                    new EntityNotFoundError($"Inventory item {itemId} not found."));
            }
            if (afterItemId == itemId)
            {
                return Result.Ok(item);
            }

            var section = InventoryItems
                .Where(i => i.IsActive && i.Id != item.Id)
                .OrderBy(i => i.SortOrder)
                .ToList();

            var afterItem = afterItemId == 0
                ? null
                : section.FirstOrDefault(i => i.Id == afterItemId);
            var beforeItem = afterItem is not null
                ? section.FirstOrDefault(i => i.SortOrder > afterItem.SortOrder)
                : null;

            int newSortOrder;
            if (afterItem is null)
            {
                if (section.Count == 0)
                {
                    newSortOrder = SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap;
                }
                else
                {
                    newSortOrder = section[0].SortOrder - SortOrderCalculator.DefaultGap;
                }
            }
            else if (beforeItem is null)
            {
                newSortOrder = afterItem.SortOrder + SortOrderCalculator.DefaultGap;
            }
            else
            {
                newSortOrder = afterItem.SortOrder + (beforeItem.SortOrder - afterItem.SortOrder) / 2;
            }

            item.SortOrder = newSortOrder;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result CompactItems()
        {
            var activeItems = InventoryItems
                .Where(i => i.IsActive)
                .OrderBy(i => i.SortOrder)
                .ToList();

            if (activeItems.Count == 0)
            {
                return Result.Ok();
            }

            var (uncheckedOrders, _) = SortOrderCalculator.GenerateCompactedSortOrders(
                activeItems.Count,
                0);

            var now = DateTime.UtcNow;
            for (int i = 0; i < activeItems.Count; i++)
            {
                activeItems[i].SortOrder = uncheckedOrders[i];
                activeItems[i].UpdatedAt = now;
            }

            return Result.Ok();
        }

        private static List<IError> ValidateItemFields(string? text, string? quantity, bool requireText)
        {
            var errors = new System.Collections.Generic.List<IError>();
            if (requireText)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    errors.Add(new Error("Item text is required.")
                        .WithMetadata("Property", nameof(InventoryItem.Text)));
                }
                else if (text!.Trim().Length > InventoryItem.TextMaxLength)
                {
                    errors.Add(new Error($"Item text must be {InventoryItem.TextMaxLength} characters or fewer.")
                        .WithMetadata("Property", nameof(InventoryItem.Text)));
                }
            }
            if (quantity is not null && quantity.Trim().Length > InventoryItem.QuantityMaxLength)
            {
                errors.Add(new Error($"Item quantity must be {InventoryItem.QuantityMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(InventoryItem.Quantity)));
            }
            return errors;
        }

        private static string? NormaliseQuantity(string? quantity)
        {
            return string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim();
        }

        // Returns the sort order for a freshly placed item: single section in the unchecked
        // range. Mirrors List.ComputeAppendSortOrder but without the status branch — inventory
        // items have no checked/unchecked split.
        private int ComputeAppendSortOrder()
        {
            var section = InventoryItems
                .Where(i => i.IsActive)
                .OrderBy(i => i.SortOrder)
                .ToList();

            if (section.Count == 0)
            {
                return SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap;
            }

            return section[^1].SortOrder + SortOrderCalculator.DefaultGap;
        }
    }
}
