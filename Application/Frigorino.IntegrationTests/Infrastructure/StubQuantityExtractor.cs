using System.Text.RegularExpressions;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free extractor for integration tests. Parses a leading
// "<number><optional g/kg/ml/l> <name>" token; otherwise returns the raw text with no quantity.
//   "20 apples" -> ("apples", {20, Piece})
//   "1l milk"   -> ("milk", {1, Liter})
public sealed class StubQuantityExtractor : IQuantityExtractor
{
    private static readonly Regex Pattern = new(
        @"^(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|ml|l)?\s+(?<name>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)
    {
        var match = Pattern.Match(rawText.Trim());
        if (!match.Success)
        {
            return Task.FromResult(Result.Ok(new QuantityExtraction(rawText, (Quantity?)null)));
        }

        var value = decimal.Parse(match.Groups["num"].Value.Replace(',', '.'),
            System.Globalization.CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "kg" => QuantityUnit.Kilogram,
            "g" => QuantityUnit.Gram,
            "ml" => QuantityUnit.Milliliter,
            "l" => QuantityUnit.Liter,
            _ => QuantityUnit.Piece,
        };
        var quantity = Quantity.Create(value, unit).Value;
        return Task.FromResult(Result.Ok(
            new QuantityExtraction(match.Groups["name"].Value.Trim(), quantity)));
    }
}
