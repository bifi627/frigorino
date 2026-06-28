using FluentResults;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Recipes
{
    public sealed record PreviewRecipeImportRequest(string Url);

    public sealed record RecipeImportPreviewResponse(string Name, string? ImageUrl);

    public static class PreviewRecipeImportEndpoint
    {
        public static IEndpointRouteBuilder MapPreviewRecipeImport(this IEndpointRouteBuilder app)
        {
            app.MapPost("import/preview", Handle)
               .WithName("PreviewRecipeImport")
               .Produces<RecipeImportPreviewResponse>()
               .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
               .ProducesValidationProblem();
            return app;
        }

        // Read-only peek: fetch + parse only (no household scope, no persistence). Reuses the same
        // cache + error codes as the real import (ImportRecipe.cs); the real import re-runs ImportAsync
        // and hits the cache, so no double network fetch.
        private static async Task<Results<Ok<RecipeImportPreviewResponse>, ValidationProblem, ProblemHttpResult>> Handle(
            PreviewRecipeImportRequest request,
            RecipeImportService importService,
            CancellationToken ct)
        {
            var import = await importService.ImportAsync(request.Url, ct);
            if (import.IsFailed)
            {
                var code = import.Errors[0].Metadata.TryGetValue("code", out var c) ? c?.ToString() : null;
                if (code == "invalid_url")
                {
                    return new Error("Enter a valid http(s) URL.").WithProperty("Url").ToValidationProblemResult();
                }
                return TypedResults.Problem(
                    detail: import.Errors[0].Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["code"] = code });
            }

            return TypedResults.Ok(new RecipeImportPreviewResponse(import.Value.Name, import.Value.ImageUrl));
        }
    }
}
