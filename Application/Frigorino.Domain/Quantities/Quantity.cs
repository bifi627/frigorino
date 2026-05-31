using FluentResults;

namespace Frigorino.Domain.Quantities
{
    // Pure domain value object: a quantity on a list item. Persisted as two flat nullable columns
    // on ListItem (QuantityValue + QuantityUnit), not an EF owned type — mirrors ExpiryProfile.
    // Both columns are set together or both null (the "no quantity" state); the List aggregate
    // enforces that invariant.
    public readonly record struct Quantity
    {
        public decimal Value { get; }
        public QuantityUnit Unit { get; }

        private Quantity(decimal value, QuantityUnit unit)
        {
            Value = value;
            Unit = unit;
        }

        // Invariant: Value must be strictly positive (decimal is always finite).
        public static Result<Quantity> Create(decimal value, QuantityUnit unit)
        {
            if (value <= 0)
            {
                return Result.Fail<Quantity>(
                    new Error("Quantity value must be greater than zero.")
                        .WithMetadata("Property", nameof(Value)));
            }

            return Result.Ok(new Quantity(value, unit));
        }
    }
}
