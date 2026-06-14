using FluentResults;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class Recipe
    {
        public const int NameMaxLength = 255;
        public const int DescriptionMaxLength = 1000;
        public const int ServingsMax = 99;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Servings { get; set; }
        public int HouseholdId { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public Household Household { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public ICollection<RecipeItem> Items { get; set; } = new List<RecipeItem>();
        public ICollection<RecipeSection> Sections { get; set; } = new List<RecipeSection>();

        public static Result<Recipe> Create(string name, string? description, int householdId, string createdByUserId, int? servings = null)
        {
            var errors = ValidateMetadata(name, description, servings);
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.").WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(createdByUserId))
            {
                errors.Add(new Error("Creator user id is required.").WithMetadata("Property", nameof(CreatedByUserId)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<Recipe>(errors);
            }

            var now = DateTime.UtcNow;
            return Result.Ok(new Recipe
            {
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Servings = servings,
                HouseholdId = householdId,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            });
        }

        public bool CanBeManagedBy(string callerUserId, HouseholdRole callerRole)
        {
            return CreatedByUserId == callerUserId || callerRole >= HouseholdRole.Admin;
        }

        public Result Update(string callerUserId, HouseholdRole callerRole, string name, string? description, int? servings = null)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(new AccessDeniedError("Only the recipe creator or an admin can edit this recipe."));
            }

            var errors = ValidateMetadata(name, description, servings);
            if (errors.Count > 0)
            {
                return Result.Fail(errors);
            }

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            Servings = servings;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result SoftDelete(string callerUserId, HouseholdRole callerRole)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(new AccessDeniedError("Only the recipe creator or an admin can delete this recipe."));
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // ---- RecipeSection coordination (collaborative — any member; no role gate) ----

        public Result<RecipeSection> AddSection(string? name, string? description)
        {
            var errors = ValidateSectionMetadata(name, description);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeSection>(errors);
            }

            var now = DateTime.UtcNow;
            var section = new RecipeSection
            {
                RecipeId = Id,
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Rank = ComputeAppendSectionRank(),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            Sections.Add(section);
            return Result.Ok(section);
        }

        public Result<RecipeSection> UpdateSection(int sectionId, string? name, string? description)
        {
            var section = Sections.FirstOrDefault(s => s.Id == sectionId && s.IsActive);
            if (section is null)
            {
                return Result.Fail<RecipeSection>(new EntityNotFoundError($"Recipe section {sectionId} not found."));
            }

            var errors = ValidateSectionMetadata(name, description);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeSection>(errors);
            }

            section.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            section.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            section.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(section);
        }

        // Cascade soft-delete: removes the section and ALL its active items. Blocked when it is the
        // last active section — a recipe always keeps at least one.
        public Result RemoveSection(int sectionId)
        {
            var section = Sections.FirstOrDefault(s => s.Id == sectionId && s.IsActive);
            if (section is null)
            {
                return Result.Fail(new EntityNotFoundError($"Recipe section {sectionId} not found."));
            }

            var activeSectionCount = Sections.Count(s => s.IsActive);
            if (activeSectionCount <= 1)
            {
                return Result.Fail(new Error("A recipe must keep at least one section.")
                    .WithMetadata("Property", nameof(Sections)));
            }

            var now = DateTime.UtcNow;
            section.IsActive = false;
            section.UpdatedAt = now;
            foreach (var item in Items.Where(i => i.SectionId == sectionId && i.IsActive))
            {
                item.IsActive = false;
                item.UpdatedAt = now;
            }
            return Result.Ok();
        }

        // Undo of a cascade delete: reactivates the section and every inactive item in it. Keeps the
        // section's ORIGINAL rank (re-mint is a separate step if it collides — see
        // ReplaceRestoredSectionRank). Note: this revives ALL inactive items in the section, including
        // any deleted individually before the cascade; acceptable because undo follows the delete
        // immediately and soft-deleted items are purged on the next cold start.
        public Result<RecipeSection> RestoreSection(int sectionId)
        {
            var section = Sections.FirstOrDefault(s => s.Id == sectionId && !s.IsActive);
            if (section is null)
            {
                return Result.Fail<RecipeSection>(new EntityNotFoundError($"Recipe section {sectionId} not found."));
            }

            var now = DateTime.UtcNow;
            section.IsActive = true;
            section.UpdatedAt = now;

            // Revive the section's items. Two inactive items in the section can share a rank — e.g. an
            // item deleted individually leaves its rank free, a sibling is reordered onto it, then the
            // whole section is cascade-deleted. Reactivating both verbatim would put two ACTIVE items at
            // the same rank → 23505 on UX_RecipeItems_SectionId_Rank_Active, which the restore slice's
            // RankRetry cannot recover (it only re-mints the section rank). So de-collide as we revive:
            // append a fresh rank for any item whose rank is already taken by a now-active sibling.
            var toRevive = Items.Where(i => i.SectionId == sectionId && !i.IsActive).ToList();
            foreach (var item in toRevive)
            {
                item.IsActive = true;
                item.UpdatedAt = now;
                var rankTaken = Items.Any(o => o.IsActive && o.Id != item.Id && o.SectionId == sectionId
                    && string.CompareOrdinal(o.Rank, item.Rank) == 0);
                if (rankTaken)
                {
                    item.Rank = ComputeAppendRank(sectionId);
                }
            }
            return Result.Ok(section);
        }

        public Result<RecipeSection> ReplaceRestoredSectionRank(int sectionId)
        {
            var section = Sections.FirstOrDefault(s => s.Id == sectionId && s.IsActive);
            if (section is null)
            {
                return Result.Fail<RecipeSection>(new EntityNotFoundError($"Recipe section {sectionId} not found."));
            }
            section.Rank = ComputeAppendSectionRank();
            section.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(section);
        }

        public Result<RecipeSection> ReorderSection(int sectionId, int afterSectionId)
        {
            var section = Sections.FirstOrDefault(s => s.Id == sectionId && s.IsActive);
            if (section is null)
            {
                return Result.Fail<RecipeSection>(new EntityNotFoundError($"Recipe section {sectionId} not found."));
            }
            if (afterSectionId == sectionId)
            {
                return Result.Ok(section);
            }

            var others = Sections
                .Where(s => s.IsActive && s.Id != section.Id)
                .OrderBy(s => s.Rank, StringComparer.Ordinal)
                .ToList();

            var after = afterSectionId == 0 ? null : others.FirstOrDefault(s => s.Id == afterSectionId);
            var before = after is not null
                ? others.FirstOrDefault(s => string.CompareOrdinal(s.Rank, after.Rank) > 0)
                : null;

            string newRank;
            if (after is null)
            {
                newRank = others.Count == 0
                    ? FractionalIndex.GenerateKeyBetween(null, null)
                    : FractionalIndex.GenerateKeyBetween(null, others[0].Rank);
            }
            else if (before is null)
            {
                newRank = FractionalIndex.GenerateKeyBetween(after.Rank, null);
            }
            else
            {
                newRank = FractionalIndex.GenerateKeyBetween(after.Rank, before.Rank);
            }

            section.Rank = newRank;
            section.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(section);
        }

        // ---- RecipeItem coordination (collaborative — any member; no role gate) ----

        public Result<RecipeItem> AddItem(int sectionId, string text, Quantity? quantity, string? comment)
        {
            var section = Sections.FirstOrDefault(s => s.Id == sectionId && s.IsActive);
            if (section is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe section {sectionId} not found."));
            }

            var errors = ValidateItemText(text, requireText: true);
            errors.AddRange(ValidateComment(comment));
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new RecipeItem
            {
                RecipeId = Id,
                SectionId = sectionId,
                Text = text.Trim(),
                Comment = NormalizeComment(comment),
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                Rank = ComputeAppendRank(sectionId),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            Items.Add(item);
            return Result.Ok(item);
        }

        public Result<RecipeItem> UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, string? comment)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }

            if (text is null && quantity is null && !clearQuantity && comment is null)
            {
                return Result.Fail<RecipeItem>(
                    new Error("Update request must set at least one field.").WithMetadata("Property", string.Empty));
            }

            var errors = ValidateItemText(text, requireText: text is not null);
            if (comment is not null)
            {
                errors.AddRange(ValidateComment(comment));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeItem>(errors);
            }

            if (text is not null)
            {
                item.Text = text.Trim();
            }
            if (clearQuantity)
            {
                item.QuantityValue = null;
                item.QuantityUnit = null;
            }
            else if (quantity is not null)
            {
                item.QuantityValue = quantity.Value.Value;
                item.QuantityUnit = quantity.Value.Unit;
            }
            if (comment is not null)
            {
                item.Comment = NormalizeComment(comment);
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result RemoveItem(int itemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result<RecipeItem> RestoreItem(int itemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && !i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result<RecipeItem> ReplaceRestoredItemRank(int itemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            item.Rank = ComputeAppendRank(item.SectionId);
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result<RecipeItem> ReorderItem(int itemId, int afterItemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            if (afterItemId == itemId)
            {
                return Result.Ok(item);
            }

            var section = Items
                .Where(i => i.IsActive && i.Id != item.Id && i.SectionId == item.SectionId)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            var afterItem = afterItemId == 0 ? null : section.FirstOrDefault(i => i.Id == afterItemId);
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

        // Applied by the recipe quantity-extraction job: rewrite text to the clean name + set/clear
        // quantity. Skips the write (and the UpdatedAt bump that would move the revision token) when
        // nothing changed. Mirrors List.ApplyExtractedQuantity.
        public Result<RecipeItem> ApplyExtractedQuantity(int itemId, string cleanName, Quantity? quantity)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }

            var errors = ValidateItemText(cleanName, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeItem>(errors);
            }

            var trimmed = cleanName.Trim();
            var unchanged = item.Text == trimmed
                && item.QuantityValue == quantity?.Value
                && item.QuantityUnit == quantity?.Unit;
            if (unchanged)
            {
                return Result.Ok(item);
            }

            item.Text = trimmed;
            item.QuantityValue = quantity?.Value;
            item.QuantityUnit = quantity?.Unit;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        private static List<IError> ValidateSectionMetadata(string? name, string? description)
        {
            var errors = new List<IError>();
            if (name is not null && name.Trim().Length > RecipeSection.NameMaxLength)
            {
                errors.Add(new Error($"Section name must be {RecipeSection.NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeSection.Name)));
            }
            if (description is not null && description.Length > RecipeSection.DescriptionMaxLength)
            {
                errors.Add(new Error($"Section description must be {RecipeSection.DescriptionMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeSection.Description)));
            }
            return errors;
        }

        private string ComputeAppendSectionRank()
        {
            var ordered = Sections
                .Where(s => s.IsActive)
                .OrderBy(s => s.Rank, StringComparer.Ordinal)
                .ToList();
            return ordered.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(ordered[^1].Rank, null);
        }

        private static List<IError> ValidateMetadata(string name, string? description, int? servings)
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Recipe name is required.").WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Recipe name must be {NameMaxLength} characters or fewer.").WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"Recipe description must be {DescriptionMaxLength} characters or fewer.").WithMetadata("Property", nameof(Description)));
            }
            if (servings is not null && (servings < 1 || servings > ServingsMax))
            {
                errors.Add(new Error($"Servings must be between 1 and {ServingsMax}.").WithMetadata("Property", nameof(Servings)));
            }
            return errors;
        }

        private static List<IError> ValidateItemText(string? text, bool requireText)
        {
            var errors = new List<IError>();
            if (requireText)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    errors.Add(new Error("Item text is required.").WithMetadata("Property", nameof(RecipeItem.Text)));
                }
                else if (text!.Trim().Length > RecipeItem.TextMaxLength)
                {
                    errors.Add(new Error($"Item text must be {RecipeItem.TextMaxLength} characters or fewer.").WithMetadata("Property", nameof(RecipeItem.Text)));
                }
            }
            return errors;
        }

        private static List<IError> ValidateComment(string? comment)
        {
            var errors = new List<IError>();
            var trimmed = NormalizeComment(comment);
            if (trimmed is not null && trimmed.Length > RecipeItem.CommentMaxLength)
            {
                errors.Add(new Error($"Item comment must be {RecipeItem.CommentMaxLength} characters or fewer.").WithMetadata("Property", nameof(RecipeItem.Comment)));
            }
            return errors;
        }

        private static string? NormalizeComment(string? comment)
            => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        private string ComputeAppendRank(int sectionId)
        {
            var section = Items
                .Where(i => i.IsActive && i.SectionId == sectionId)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();
            return section.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(section[^1].Rank, null);
        }
    }
}
