using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Frigorino.Features.Results
{
    public static class ResultExtensions
    {
        public const string PropertyMetadataKey = "Property";

        public static ValidationProblem ToValidationProblem(this IResultBase result)
        {
            var errors = result.Errors
                .GroupBy(e => e.Metadata.TryGetValue(PropertyMetadataKey, out var p)
                    ? p?.ToString() ?? string.Empty
                    : string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        public static Error WithProperty(this Error error, string propertyName)
        {
            return (Error)error.WithMetadata(PropertyMetadataKey, propertyName);
        }

        // Convenience for slices that build a one-off ValidationProblem from a single Error without
        // going through a Result<T> (e.g. transport-level guards).
        public static ValidationProblem ToValidationProblemResult(this Error error)
        {
            var key = error.Metadata.TryGetValue(PropertyMetadataKey, out var p)
                ? p?.ToString() ?? string.Empty
                : string.Empty;
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { [key] = new[] { error.Message } });
        }
    }
}
