using FluentResults;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;

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

        // Edit permission for the inventory and anything owned by it (settings, items metadata):
        // the creator, or an Admin+. Single home for the policy so Update/SoftDelete and the
        // settings slice share one gate.
        public bool CanBeManagedBy(string callerUserId, HouseholdRole callerRole)
        {
            return CreatedByUserId == callerUserId || callerRole >= HouseholdRole.Admin;
        }

        // Aggregate-internal mutation. Edit permission is creator-OR-Admin+ — the legacy rule
        // preserved from InventoryService. Caller's role is passed in because it lives on
        // UserHousehold (a different aggregate) and the handler resolves it once.
        public Result Update(string callerUserId, HouseholdRole callerRole, string name, string? description)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
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
            if (!CanBeManagedBy(callerUserId, callerRole))
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
        // Item-level operations live on Inventory because ordering is a multi-row invariant of the
        // aggregate. Order is a server-minted fractional-index string Rank (see FractionalIndex),
        // a single section — items have no status flag, unlike ListItem. Items intentionally have
        // no role gate: any active household member can add / update / reorder / delete, matching
        // the legacy InventoryItemService behaviour. The handler enforces membership; the aggregate
        // doesn't take callerRole.

        public Result<InventoryItem> AddItem(string text, Quantity? quantity, DateOnly? expiryDate)
        {
            var errors = ValidateItemText(text, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<InventoryItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new InventoryItem
            {
                InventoryId = Id,
                Text = text.Trim(),
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                ExpiryDate = expiryDate,
                Rank = ComputeAppendRank(),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            InventoryItems.Add(item);
            return Result.Ok(item);
        }

        // Text preserves on null (caller intent: "leave existing value"). Quantity is tri-state:
        // clearQuantity removes it; a non-null quantity writes both columns; null means "preserve".
        // ExpiryDate is intentionally write-through — null means "clear the value", because it is a
        // first-class field the user explicitly sets/unsets via a date picker. The asymmetry is
        // deliberate.
        public Result<InventoryItem> UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, DateOnly? expiryDate)
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
            var errors = ValidateItemText(text, requireText: text is not null);
            if (errors.Count > 0)
            {
                return Result.Fail<InventoryItem>(errors);
            }

            if (text is not null)
            {
                item.Text = text.Trim();
            }
            // clearQuantity removes it; otherwise quantity == null means "preserve" and a non-null
            // quantity writes both columns. (clearQuantity wins over a stray quantity value.)
            if (clearQuantity)
            {
                item.QuantityValue = null;
                item.QuantityUnit = null;
            }
            else if (quantity is not null)
            {
                var q = quantity.Value;
                item.QuantityValue = q.Value;
                item.QuantityUnit = q.Unit;
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

        public Result<InventoryItem> RestoreItem(int itemId)
        {
            var item = InventoryItems.FirstOrDefault(i => i.Id == itemId && !i.IsActive);
            if (item is null)
            {
                return Result.Fail<InventoryItem>(
                    new EntityNotFoundError($"Inventory item {itemId} not found."));
            }

            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
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
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            var afterItem = afterItemId == 0
                ? null
                : section.FirstOrDefault(i => i.Id == afterItemId);
            var beforeItem = afterItem is not null
                ? section.FirstOrDefault(i => string.CompareOrdinal(i.Rank, afterItem.Rank) > 0)
                : null;

            string newRank;
            if (afterItem is null)
            {
                newRank = section.Count == 0
                    ? FractionalIndex.GenerateKeyBetween(null, null)
                    : FractionalIndex.GenerateKeyBetween(null, section[0].Rank);
            }
            else if (beforeItem is null)
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, null);
            }
            else
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, beforeItem.Rank);
            }

            item.Rank = newRank;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        private static List<IError> ValidateItemText(string? text, bool requireText)
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
            return errors;
        }

        // Returns the rank for a freshly placed item: append after the last item in the single
        // section. Mirrors List.ComputeAppendRank but without the status branch — inventory items
        // have no checked/unchecked split.
        private string ComputeAppendRank()
        {
            var section = InventoryItems
                .Where(i => i.IsActive)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            return section.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(section[^1].Rank, null);
        }
    }
}
