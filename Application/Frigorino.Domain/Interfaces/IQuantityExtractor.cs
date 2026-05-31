using FluentResults;
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // The ONLY quantity-extraction AI abstraction. The OpenAI SDK never crosses this boundary.
    // Transient/API errors return Result.Fail (the job drops the work item — lossy by design);
    // a model refusal / no-quantity result is returned as Ok with the raw text as CleanName.
    public interface IQuantityExtractor
    {
        Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct);
    }
}
