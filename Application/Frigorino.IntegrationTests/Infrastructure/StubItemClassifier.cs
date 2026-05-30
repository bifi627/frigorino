using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free classifier for integration tests:
//   "milk"/"milch"      → Food, AI-recommended 7-day shelf life
//   "soap"/"spülmittel" → HouseholdSupply, non-perishable
//   "call"/"anruf"      → Other, non-perishable (a task, not a stockable product)
//   everything else     → Food, non-perishable
public sealed class StubItemClassifier : IItemClassifier
{
    public int Version => 1;

    public Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
    {
        ProductClassification result;
        if (normalizedName.Contains("milk") || normalizedName.Contains("milch"))
        {
            result = new ProductClassification(
                ProductCategory.Food, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value);
        }
        else if (normalizedName.Contains("soap") || normalizedName.Contains("spülmittel"))
        {
            result = new ProductClassification(ProductCategory.HouseholdSupply, ExpiryProfile.NonPerishable);
        }
        else if (normalizedName.Contains("call") || normalizedName.Contains("anruf"))
        {
            result = new ProductClassification(ProductCategory.Other, ExpiryProfile.NonPerishable);
        }
        else
        {
            result = new ProductClassification(ProductCategory.Food, ExpiryProfile.NonPerishable);
        }

        return Task.FromResult(Result.Ok(result));
    }
}
