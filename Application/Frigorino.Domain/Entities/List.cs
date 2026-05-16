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

        // ------- ListItem coordination -------
        //
        // Item-level operations live on List because sort-order is a multi-row invariant of the
        // aggregate (two parallel ranges per list — unchecked 1M..9M, checked 10M..19M). Items
        // intentionally have no role gate: any active household member can add / toggle /
        // reorder / delete / compact, matching the collaborative grocery-list UX. The handler
        // enforces membership; the aggregate doesn't take callerRole.

        public Result<ListItem> AddItem(string text, string? quantity)
        {
            var errors = ValidateItemFields(text, quantity, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new ListItem
            {
                ListId = Id,
                Text = text.Trim(),
                Quantity = NormaliseQuantity(quantity),
                Status = false,
                SortOrder = ComputeAppendSortOrder(targetStatus: false),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            ListItems.Add(item);
            return Result.Ok(item);
        }

        public Result<ListItem> UpdateItem(int itemId, string? text, string? quantity, bool? status)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            // Text is required only when the caller actually supplies a value — null means
            // "preserve existing". Empty/whitespace, on the other hand, is a real attempt at
            // a blank text and is rejected.
            var errors = ValidateItemFields(text, quantity, requireText: text is not null);
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            if (status.HasValue && item.Status != status.Value)
            {
                item.SortOrder = ComputeAppendSortOrder(targetStatus: status.Value);
                item.Status = status.Value;
            }

            if (text is not null)
            {
                item.Text = text.Trim();
            }
            if (quantity is not null)
            {
                item.Quantity = NormaliseQuantity(quantity);
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result RemoveItem(int itemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result<ListItem> ToggleItemStatus(int itemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            var newStatus = !item.Status;
            item.SortOrder = ComputeAppendSortOrder(targetStatus: newStatus);
            item.Status = newStatus;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // AfterItemId == 0 means "move to the top of the current section". An afterItemId that
        // doesn't resolve to an active sibling in the same status section silently falls back to
        // top-of-section — preserves the legacy ListItemService wire contract the frontend's
        // optimistic UI depends on. Self-anchor (afterItemId == itemId) is a no-op.
        public Result<ListItem> ReorderItem(int itemId, int afterItemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }
            if (afterItemId == itemId)
            {
                return Result.Ok(item);
            }

            var section = ListItems
                .Where(i => i.IsActive && i.Status == item.Status && i.Id != item.Id)
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
                    newSortOrder = item.Status
                        ? SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap
                        : SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap;
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
            var uncheckedItems = ListItems
                .Where(i => i.IsActive && !i.Status)
                .OrderBy(i => i.SortOrder)
                .ToList();
            var checkedItems = ListItems
                .Where(i => i.IsActive && i.Status)
                .OrderBy(i => i.SortOrder)
                .ToList();

            if (uncheckedItems.Count == 0 && checkedItems.Count == 0)
            {
                return Result.Ok();
            }

            var (uncheckedOrders, checkedOrders) = SortOrderCalculator.GenerateCompactedSortOrders(
                uncheckedItems.Count,
                checkedItems.Count);

            var now = DateTime.UtcNow;
            for (int i = 0; i < uncheckedItems.Count; i++)
            {
                uncheckedItems[i].SortOrder = uncheckedOrders[i];
                uncheckedItems[i].UpdatedAt = now;
            }
            for (int i = 0; i < checkedItems.Count; i++)
            {
                checkedItems[i].SortOrder = checkedOrders[i];
                checkedItems[i].UpdatedAt = now;
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
                        .WithMetadata("Property", nameof(ListItem.Text)));
                }
                else if (text!.Trim().Length > ListItem.TextMaxLength)
                {
                    errors.Add(new Error($"Item text must be {ListItem.TextMaxLength} characters or fewer.")
                        .WithMetadata("Property", nameof(ListItem.Text)));
                }
            }
            if (quantity is not null && quantity.Trim().Length > ListItem.QuantityMaxLength)
            {
                errors.Add(new Error($"Item quantity must be {ListItem.QuantityMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(ListItem.Quantity)));
            }
            return errors;
        }

        private static string? NormaliseQuantity(string? quantity)
        {
            return string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim();
        }

        // Returns the sort order for a freshly placed item in `targetStatus`'s section:
        //   - empty section: MinRange + DefaultGap
        //   - unchecked: last + DefaultGap (append below the last existing unchecked)
        //   - checked  : first - DefaultGap (prepend above the first existing checked)
        // Mirrors the section-anchored branches of SortOrderCalculator but skips the calculator's
        // multi-branch generality (which exists for the legacy Inventory layer's per-position
        // inserts and is on its way out with that migration).
        private int ComputeAppendSortOrder(bool targetStatus)
        {
            var section = ListItems
                .Where(i => i.IsActive && i.Status == targetStatus)
                .OrderBy(i => i.SortOrder)
                .ToList();

            if (section.Count == 0)
            {
                return (targetStatus
                    ? SortOrderCalculator.CheckedMinRange
                    : SortOrderCalculator.UncheckedMinRange)
                    + SortOrderCalculator.DefaultGap;
            }

            return targetStatus
                ? section[0].SortOrder - SortOrderCalculator.DefaultGap
                : section[^1].SortOrder + SortOrderCalculator.DefaultGap;
        }
    }
}
