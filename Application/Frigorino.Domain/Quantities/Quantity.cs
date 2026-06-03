using FluentResults;

namespace Frigorino.Domain.Quantities
{
    // Pure domain value object: a quantity on a list item. Persisted as two flat nullable columns
    // on ListItem (QuantityValue + QuantityUnit), not an EF owned type — mirrors ExpiryProfile.
    // Both columns are set together or both null (the "no quantity" state); the List aggregate
    // enforces that invariant.
    public readonly record struct Quantity
    {
        // Persisted as numeric(12,3): 9 integer digits + 3 fractional. Cap the magnitude and
        // round to the stored scale in the factory so an out-of-range value fails validation
        // here (→ ValidationProblem / 400) instead of throwing numeric_field_overflow at
        // SaveChanges (→ 500), and so the in-memory VO matches exactly what the DB holds.
        public const decimal MaxValue = 999_999_999.999m;
        private const int Scale = 3;

        public decimal Value { get; }
        public QuantityUnit Unit { get; }

        private Quantity(decimal value, QuantityUnit unit)
        {
            Value = value;
            Unit = unit;
        }

        // Invariant: Value must be strictly positive and within the persisted numeric(12,3) range
        // (decimal is always finite). Values are rounded to 3 decimals; anything that rounds to
        // zero is rejected as non-positive.
        public static Result<Quantity> Create(decimal value, QuantityUnit unit)
        {
            var normalized = Math.Round(value, Scale, MidpointRounding.AwayFromZero);

            if (normalized <= 0)
            {
                return Result.Fail<Quantity>(
                    new Error("Quantity value must be greater than zero.")
                        .WithMetadata("Property", nameof(Value)));
            }

            if (normalized > MaxValue)
            {
                return Result.Fail<Quantity>(
                    new Error($"Quantity value must not exceed {MaxValue}.")
                        .WithMetadata("Property", nameof(Value)));
            }

            return Result.Ok(new Quantity(normalized, unit));
        }
    }
}
