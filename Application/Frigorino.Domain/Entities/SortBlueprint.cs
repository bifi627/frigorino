using FluentResults;
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Entities
{
    // Household-scoped, named ordered subset of supermarket aisles ("walk-order"). Applying a
    // blueprint to a list reorders the list's unchecked items by these category ranks. Any
    // household member may curate, reorder and apply blueprints (no role gate). Sentinels
    // (Unknown/Other) can never be ranked — items in those categories (or unclassified) sink
    // to the bottom on apply.
    public class SortBlueprint
    {
        // Shares List.NameMaxLength's width so no new column-width constant / migration churn.
        public const int NameMaxLength = 255;
        public const string DefaultName = "Supermarket";

        // Canonical full walk-order over all 23 real aisles, used to seed the starter blueprint.
        private static readonly ProductCategory[] DefaultOrder =
        {
            ProductCategory.Produce, ProductCategory.Bakery, ProductCategory.DeliAndColdCuts,
            ProductCategory.Meat, ProductCategory.Fish, ProductCategory.DairyAndEggs,
            ProductCategory.Cheese, ProductCategory.Frozen, ProductCategory.Cereal,
            ProductCategory.Pantry, ProductCategory.CannedGoods, ProductCategory.Sauces,
            ProductCategory.OilsAndVinegar, ProductCategory.Spices, ProductCategory.Spreads,
            ProductCategory.Snacks, ProductCategory.Sweets, ProductCategory.Beverages,
            ProductCategory.Alcohol, ProductCategory.HouseholdAndCleaning,
            ProductCategory.HealthAndBeauty, ProductCategory.Baby, ProductCategory.Pet,
        };

        public int Id { get; set; }
        public int HouseholdId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Household Household { get; set; } = null!;
        public ICollection<SortBlueprintCategory> Categories { get; set; } = new List<SortBlueprintCategory>();

        public static Result<SortBlueprint> Create(
            int householdId, string name, IReadOnlyList<ProductCategory> orderedCategories)
        {
            var errors = Validate(householdId, name, orderedCategories);
            if (errors.Count > 0)
            {
                return Result.Fail<SortBlueprint>(errors);
            }

            var blueprint = new SortBlueprint
            {
                HouseholdId = householdId,
                Name = name.Trim(),
                IsActive = true,
            };
            blueprint.ReplaceCategories(orderedCategories);
            return Result.Ok(blueprint);
        }

        public Result Update(string name, IReadOnlyList<ProductCategory> orderedCategories)
        {
            var errors = Validate(HouseholdId, name, orderedCategories);
            if (errors.Count > 0)
            {
                return Result.Fail(errors);
            }

            Name = name.Trim();
            ReplaceCategories(orderedCategories);
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result SoftDelete()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // Undo a soft-delete (powers the delete toast's "Undo" action). Reactivates in place;
        // no rank concerns — blueprints carry no per-section uniqueness like list items do.
        public Result Restore()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // System seed (no role gate): the starter "Supermarket" blueprint covering every aisle.
        public static SortBlueprint CreateDefault(int householdId)
        {
            var blueprint = new SortBlueprint
            {
                HouseholdId = householdId,
                Name = DefaultName,
                IsActive = true,
            };
            blueprint.ReplaceCategories(DefaultOrder);
            return blueprint;
        }

        public IReadOnlyList<ProductCategory> OrderedCategories()
        {
            return Categories.OrderBy(c => c.OrderIndex).Select(c => c.Category).ToList();
        }

        private void ReplaceCategories(IReadOnlyList<ProductCategory> orderedCategories)
        {
            Categories.Clear();
            for (var i = 0; i < orderedCategories.Count; i++)
            {
                Categories.Add(new SortBlueprintCategory
                {
                    Category = orderedCategories[i],
                    OrderIndex = i,
                });
            }
        }

        private static List<IError> Validate(int householdId, string name, IReadOnlyList<ProductCategory> orderedCategories)
        {
            var errors = new List<IError>();
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.")
                    .WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Blueprint name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Blueprint name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (orderedCategories.Count == 0)
            {
                errors.Add(new Error("A blueprint must include at least one aisle.")
                    .WithMetadata("Property", nameof(Categories)));
            }
            if (orderedCategories.Distinct().Count() != orderedCategories.Count)
            {
                errors.Add(new Error("A blueprint cannot list the same aisle twice.")
                    .WithMetadata("Property", nameof(Categories)));
            }
            var hasSentinel = orderedCategories.Any(c => c == ProductCategory.Unknown || c == ProductCategory.Other);
            if (hasSentinel)
            {
                errors.Add(new Error("A blueprint can only contain real aisles (not Unknown or Other).")
                    .WithMetadata("Property", nameof(Categories)));
            }
            return errors;
        }
    }
}
