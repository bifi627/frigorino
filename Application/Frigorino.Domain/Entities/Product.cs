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
        public ExpiryHandling ClassificationExpiryHandling { get; set; }
        public int? ClassificationShelfLifeDays { get; set; }
        public int ClassifierVersion { get; set; }

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
            ClassificationExpiryHandling = classification.Expiry.Handling;
            ClassificationShelfLifeDays = classification.Expiry.ShelfLifeDays;
            ClassifierVersion = classifierVersion;
        }

        // Effective expiry the rest of the app reads. Minimal today (Classification only); becomes
        // Override ?? Classification when override columns land. Safe .Value — columns are written
        // through a validated ExpiryProfile.
        public ExpiryProfile EffectiveExpiry =>
            ExpiryProfile.Create(ClassificationExpiryHandling, ClassificationShelfLifeDays).Value;
    }
}
