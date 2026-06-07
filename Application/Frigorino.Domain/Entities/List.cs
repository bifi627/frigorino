using FluentResults;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Files;
using Frigorino.Domain.Products;
using Frigorino.Domain.Quantities;

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
        // Item-level operations live on List because ordering is a multi-row invariant of the
        // aggregate. Order is a server-minted fractional-index string Rank (see FractionalIndex),
        // scoped per (List, Status) — two independent sections, unchecked and checked. Items
        // intentionally have no role gate: any active household member can add / toggle /
        // reorder / delete, matching the collaborative grocery-list UX. The handler enforces
        // membership; the aggregate doesn't take callerRole.

        public Result<ListItem> AddItem(string text, Quantity? quantity = null, string? comment = null)
        {
            var errors = ValidateItemText(text, requireText: true);
            errors.AddRange(ValidateComment(comment));
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new ListItem
            {
                ListId = Id,
                Text = text.Trim(),
                Comment = NormalizeComment(comment),
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                Status = false,
                Rank = ComputeAppendRank(targetStatus: false),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            ListItems.Add(item);
            return Result.Ok(item);
        }

        public Result<ListItem> AddMediaItem(ListItemType type, string? caption, StoredFile file)
        {
            var errors = new System.Collections.Generic.List<IError>();

            if (type != ListItemType.Image && type != ListItemType.Document)
            {
                errors.Add(new Error("Media item type must be Image or Document.")
                    .WithMetadata("Property", nameof(ListItem.Type)));
                // Bail early: the allowlist branch below dereferences `type`.
                return Result.Fail<ListItem>(errors);
            }

            var allowed = type == ListItemType.Image
                ? ListItem.ImageContentTypes
                : ListItem.DocumentContentTypes;
            if (string.IsNullOrWhiteSpace(file.ContentType) || !allowed.Contains(file.ContentType))
            {
                errors.Add(new Error($"Content type '{file.ContentType}' is not allowed for {type} items.")
                    .WithMetadata("Property", nameof(ListItem.ContentType)));
            }

            if (string.IsNullOrWhiteSpace(file.StorageKey) || file.StorageKey.Length > ListItem.StorageKeyMaxLength)
            {
                errors.Add(new Error($"Storage key is required and must be {ListItem.StorageKeyMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(ListItem.StorageKey)));
            }

            if (string.IsNullOrWhiteSpace(file.OriginalFileName)
                || file.OriginalFileName.Length > ListItem.OriginalFileNameMaxLength)
            {
                errors.Add(new Error($"File name is required and must be {ListItem.OriginalFileNameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(ListItem.OriginalFileName)));
            }

            if (file.SizeBytes <= 0 || file.SizeBytes > ListItem.MaxFileSizeBytes)
            {
                errors.Add(new Error($"File size must be between 1 and {ListItem.MaxFileSizeBytes} bytes.")
                    .WithMetadata("Property", nameof(ListItem.FileSizeBytes)));
            }

            // Type/thumbnail invariant: images carry a thumbnail, documents must not.
            var hasThumbnail = !string.IsNullOrWhiteSpace(file.ThumbnailKey);
            if (type == ListItemType.Image && !hasThumbnail)
            {
                errors.Add(new Error("Image items require a thumbnail key.")
                    .WithMetadata("Property", nameof(ListItem.ThumbnailStorageKey)));
            }
            else if (type == ListItemType.Document && hasThumbnail)
            {
                errors.Add(new Error("Document items must not have a thumbnail key.")
                    .WithMetadata("Property", nameof(ListItem.ThumbnailStorageKey)));
            }

            errors.AddRange(ValidateComment(caption));

            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new ListItem
            {
                ListId = Id,
                Type = type,
                Text = "",
                Comment = NormalizeComment(caption),
                StorageKey = file.StorageKey,
                ThumbnailStorageKey = file.ThumbnailKey,
                ContentType = file.ContentType,
                OriginalFileName = file.OriginalFileName,
                FileSizeBytes = file.SizeBytes,
                Status = false,
                Rank = ComputeAppendRank(targetStatus: false),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            ListItems.Add(item);
            return Result.Ok(item);
        }

        public Result<ListItem> UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, bool? status, string? comment = null)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            // text/quantity/status/comment are "preserve on null"; clearQuantity is the explicit "remove
            // the quantity" intent. With none of them set the payload is a guaranteed no-op —
            // reject it rather than returning 200 OK on garbage.
            if (text is null && quantity is null && !clearQuantity && status is null && comment is null)
            {
                return Result.Fail<ListItem>(
                    new Error("Update request must set at least one field.")
                        .WithMetadata("Property", string.Empty));
            }

            // Media items (Image/Document) carry no Text or quantity by design — only the caption
            // (Comment) and status are editable. Reject any attempt to mutate text/quantity on them
            // so the clean-separation invariant holds regardless of client.
            if (item.Type != ListItemType.Text && (text is not null || quantity is not null || clearQuantity))
            {
                return Result.Fail<ListItem>(
                    new Error("Only the caption can be edited on a media item.")
                        .WithMetadata("Property", string.Empty));
            }

            var errors = ValidateItemText(text, requireText: text is not null);
            if (comment is not null)
            {
                errors.AddRange(ValidateComment(comment));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            if (status.HasValue && item.Status != status.Value)
            {
                item.Rank = ComputeAppendRank(targetStatus: status.Value);
                item.Status = status.Value;
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

            // comment == null means "preserve"; an empty/whitespace string clears it; otherwise set.
            if (comment is not null)
            {
                item.Comment = NormalizeComment(comment);
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // Applied by the quantity-extraction job: overwrite the item's text with the extracted
        // clean name and set (or clear) the structured quantity authoritatively.
        public Result<ListItem> ApplyExtractedQuantity(int itemId, string cleanName, Quantity? quantity)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            var errors = ValidateItemText(cleanName, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            item.Text = cleanName.Trim();
            item.QuantityValue = quantity?.Value;
            item.QuantityUnit = quantity?.Unit;
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

        public Result<ListItem> RestoreItem(int itemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && !i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
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
            item.Rank = ComputeAppendRank(targetStatus: newStatus);
            item.Status = newStatus;

            // Unchecking retracts any promotion candidacy/resolution so a later re-check is a clean
            // re-evaluation — mirrors the old localStorage "uncheck removes from the batch" contract.
            if (!newStatus)
            {
                item.PromotionExpiryHandling = null;
                item.PromotionSuggestedExpiry = null;
                item.PromotionResolvedAt = null;
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // Stamps the promotion candidacy captured when the item was checked (perishable product →
        // handling + suggested expiry; non-perishable → both null) and clears any prior resolution
        // so a re-checked item becomes pending again. The handler supplies the suggestion because
        // it derives from the Product catalog (a different aggregate the entity must not touch).
        // Callers pass null handling for non-candidate (non-perishable) items — never
        // ExpiryHandling.NonPerishable — so the documented pending predicate holds.
        public Result<ListItem> ApplyPromotionSuggestion(int itemId, ExpiryHandling? handling, DateOnly? suggestedExpiry)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            item.PromotionExpiryHandling = handling;
            item.PromotionSuggestedExpiry = suggestedExpiry;
            item.PromotionResolvedAt = null;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // Marks a pending-promotion item as dealt with — promoted into inventory OR skipped (X /
        // Clear All). Idempotent: an already-resolved item is a no-op success, so two members racing
        // the same shared batch (Person A + Person B) don't error — first writer wins.
        public Result ResolvePromotion(int itemId, DateTime resolvedAt)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            if (item.PromotionResolvedAt is null)
            {
                item.PromotionResolvedAt = resolvedAt;
                item.UpdatedAt = resolvedAt;
            }
            return Result.Ok();
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

        // empty/whitespace comment is normalized to null; otherwise trimmed.
        private static string? NormalizeComment(string? comment)
        {
            return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        }

        private static List<IError> ValidateComment(string? comment)
        {
            var errors = new System.Collections.Generic.List<IError>();
            var trimmed = NormalizeComment(comment);
            if (trimmed is not null && trimmed.Length > ListItem.CommentMaxLength)
            {
                errors.Add(new Error($"Item comment must be {ListItem.CommentMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(ListItem.Comment)));
            }
            return errors;
        }

        private static List<IError> ValidateItemText(string? text, bool requireText)
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
            return errors;
        }

        // Returns the rank for a freshly placed item in `targetStatus`'s section:
        //   - unchecked: append after the last unchecked  (key between last and null)
        //   - checked  : prepend above the first checked    (key between null and first)
        //   - empty section: GenerateKeyBetween(null, null)
        private string ComputeAppendRank(bool targetStatus)
        {
            var section = ListItems
                .Where(i => i.IsActive && i.Status == targetStatus)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            if (section.Count == 0)
            {
                return FractionalIndex.GenerateKeyBetween(null, null);
            }

            return targetStatus
                ? FractionalIndex.GenerateKeyBetween(null, section[0].Rank)
                : FractionalIndex.GenerateKeyBetween(section[^1].Rank, null);
        }
    }
}
