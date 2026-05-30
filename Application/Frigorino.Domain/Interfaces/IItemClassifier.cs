using FluentResults;
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Interfaces
{
    // The ONLY AI abstraction. The OpenAI SDK never crosses this boundary into Domain/Features.
    public interface IItemClassifier
    {
        // Returns the classification for an already-normalized product name. Transient/API errors
        // return Result.Fail (the job drops the work item — lossy by design); a model refusal is
        // mapped to NonPerishable by the adapter, not surfaced as a failure.
        Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct);

        // Stamped onto Product.ClassifierVersion; bumped when the prompt/model changes to force
        // re-classification on the next reference.
        int Version { get; }
    }
}
