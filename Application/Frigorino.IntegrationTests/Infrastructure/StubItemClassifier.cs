using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free classifier for integration tests: "milk"/"milch" → Ai-recommended
// 7-day shelf life; everything else → non-perishable.
public sealed class StubItemClassifier : IItemClassifier
{
    public int Version => 1;

    public Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
    {
        var profile = normalizedName.Contains("milk") || normalizedName.Contains("milch")
            ? ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value
            : ExpiryProfile.NonPerishable;

        return Task.FromResult(Result.Ok(new ProductClassification(profile)));
    }
}
