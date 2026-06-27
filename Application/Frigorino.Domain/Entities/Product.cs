using FluentResults;
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Entities
{
    // Per-household product catalog row, keyed (HouseholdId, NormalizedName). Holds the AI
    // Classification layer as flat columns. A user Override layer is a future additive set of
    // nullable columns; EffectiveExpiry will become Override ?? Classification then.
    public class Product
    {
        public const int NormalizedNameMaxLength = 200;

        public int Id { get; set; }
        public int HouseholdId { get; set; }
        public string NormalizedName { get; set; } = string.Empty;

        // AI Classification layer (overwritten wholesale on (re)classification).
        public ProductCategory ClassificationProductCategory { get; set; }
        public ExpiryHandling ClassificationExpiryHandling { get; set; }
        public int? ClassificationShelfLifeDays { get; set; }
        public int ClassifierVersion { get; set; }

        // User Override layer (additive, nullable). Set/cleared atomically via
        // OverrideClassification / ResetToAiClassification. Presence shields the row from
        // backfill re-classification; EffectiveX prefers it over the AI Classification layer.
        public ProductCategory? OverrideProductCategory { get; set; }
        public ExpiryHandling? OverrideExpiryHandling { get; set; }
        public int? OverrideShelfLifeDays { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation: cascade-deleted with the household (configured FK-only, no Household nav).
        public Household Household { get; set; } = null!;

        // NormalizedName is expected pre-normalized by the caller (ProductName.Normalize).
        public static Result<Product> Create(
            int householdId, string normalizedName, ProductClassification classification, int classifierVersion)
        {
            var errors = new List<IError>();
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.")
                    .WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                errors.Add(new Error("Normalized name is required.")
                    .WithMetadata("Property", nameof(NormalizedName)));
            }
            else if (normalizedName.Length > NormalizedNameMaxLength)
            {
                errors.Add(new Error($"Normalized name must be {NormalizedNameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(NormalizedName)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<Product>(errors);
            }

            var product = new Product
            {
                HouseholdId = householdId,
                NormalizedName = normalizedName,
            };
            product.ApplyClassification(classification, classifierVersion);
            return Result.Ok(product);
        }

        // Overwrites the AI layer wholesale and re-stamps the version. UpdatedAt is auto-stamped
        // by ApplicationDbContext.SaveChangesAsync.
        public void ApplyClassification(ProductClassification classification, int classifierVersion)
        {
            ClassificationProductCategory = classification.Category;
            ClassificationExpiryHandling = classification.Expiry.Handling;
            ClassificationShelfLifeDays = classification.Expiry.ShelfLifeDays;
            ClassifierVersion = classifierVersion;
        }

        // User override: take ownership of this product's classification. UpdatedAt is auto-stamped
        // by ApplicationDbContext.SaveChangesAsync. The AI Classification layer is left untouched.
        public void OverrideClassification(ProductClassification classification)
        {
            OverrideProductCategory = classification.Category;
            OverrideExpiryHandling = classification.Expiry.Handling;
            OverrideShelfLifeDays = classification.Expiry.ShelfLifeDays;
        }

        // Reset to AI: drop the override; EffectiveCategory/EffectiveExpiry fall back to the
        // preserved AI layer immediately. A stale ClassifierVersion re-enters the backfill gap
        // set on the next cold start.
        public void ResetToAiClassification()
        {
            OverrideProductCategory = null;
            OverrideExpiryHandling = null;
            OverrideShelfLifeDays = null;
        }

        // Atomic: the three override columns are written/cleared together, so any one is a
        // valid presence flag.
        public bool IsOverridden => OverrideExpiryHandling.HasValue;

        // Effective category the rest of the app reads: user override wins over the AI layer.
        public ProductCategory EffectiveCategory =>
            OverrideProductCategory ?? ClassificationProductCategory;

        // Effective expiry the rest of the app reads. Expiry is taken as a WHOLE facet, not
        // column-by-column: a NonPerishable override must null the days, never fall back to the
        // AI's shelf life. Safe .Value — both layers are written through a validated ExpiryProfile.
        public ExpiryProfile EffectiveExpiry =>
            OverrideExpiryHandling.HasValue
                ? ExpiryProfile.Create(OverrideExpiryHandling.Value, OverrideShelfLifeDays).Value
                : ExpiryProfile.Create(ClassificationExpiryHandling, ClassificationShelfLifeDays).Value;
    }
}
