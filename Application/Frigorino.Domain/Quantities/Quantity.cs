using System.Globalization;
using System.Text.RegularExpressions;
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

        // Deterministic, conservative parse of the unambiguous quantity shapes (Option A): a number
        // is a quantity ONLY when it is a standalone token glued to / followed by a known metric
        // unit, or the leading bare integer count. Everything else (brand-digits like "7up"/"WD-40",
        // trailing bare integers, mid-string, container words) returns false and is left to the LLM.
        // Patterns are tried in order; first confident match wins.
        private static readonly Regex LeadingUnit = new(
            @"^\s*(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|ml|l)\b\s*(?<name>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TrailingUnit = new(
            @"^\s*(?<name>.+?)\s+(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|ml|l)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LeadingBare = new(
            @"^\s*(?<num>\d+)\s+(?<name>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UnitOnly = new(
            @"^(kg|g|ml|l)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParse(string text, out string cleanName, out Quantity quantity)
        {
            cleanName = string.Empty;
            quantity = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (var (regex, isBare) in new[]
                     {
                         (LeadingUnit, false),
                         (TrailingUnit, false),
                         (LeadingBare, true),
                     })
            {
                var match = regex.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups["name"].Value.Trim();
                // Number-only-no-product: "2kg" (empty name) or "2 kg" (name is just a unit token).
                if (name.Length == 0 || (isBare && UnitOnly.IsMatch(name)))
                {
                    continue;
                }

                var numText = match.Groups["num"].Value.Replace(',', '.');
                if (!decimal.TryParse(numText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                var unit = isBare ? QuantityUnit.Piece : MapUnit(match.Groups["unit"].Value);
                var created = Quantity.Create(value, unit);
                if (created.IsFailed)
                {
                    continue;
                }

                cleanName = name;
                quantity = created.Value;
                return true;
            }

            return false;
        }

        private static QuantityUnit MapUnit(string unit) => unit.ToLowerInvariant() switch
        {
            "g" => QuantityUnit.Gram,
            "kg" => QuantityUnit.Kilogram,
            "ml" => QuantityUnit.Milliliter,
            "l" => QuantityUnit.Liter,
            _ => QuantityUnit.Piece,
        };
    }
}
