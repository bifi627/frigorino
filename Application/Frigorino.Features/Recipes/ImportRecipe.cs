using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Features.Recipes
{
    // Name/Description are optional caller overrides: when the user has typed them on the create
    // page they take precedence over whatever the page parses to (import-as-prefill).
    public sealed record ImportRecipeRequest(string Url, string? Name = null, string? Description = null);

    public static class ImportRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapImportRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPost("import", Handle)
               .WithName("ImportRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeResponse>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
            int householdId,
            ImportRecipeRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            RecipeImportService importService,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            [FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage coverStorage,
            IImageProcessor imageProcessor,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }
            var creator = membership.User;

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

            var imported = import.Value;
            // User-typed name/description win over the parsed values; when absent the parsed values
            // are used (a successful import always carries a name — the parser skips name-less nodes).
            var effectiveName = string.IsNullOrWhiteSpace(request.Name) ? imported.Name : request.Name.Trim();
            var effectiveDescription = string.IsNullOrWhiteSpace(request.Description) ? imported.Description : request.Description.Trim();
            var creation = Recipe.Create(effectiveName, effectiveDescription, householdId, currentUser.UserId, imported.Servings);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var recipe = creation.Value;
            recipe.CreatedByUser = creator;
            recipe.AddSection(null, null); // every recipe starts with one unnamed default section
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct); // recipe + section now have real ids so AddItem can link the FK

            var section = recipe.Sections.First(s => s.IsActive);
            var routed = new List<(RecipeItem Item, ItemTextAnalysis Analysis)>();
            foreach (var ingredient in imported.Ingredients)
            {
                var analysis = ItemTextRouter.Analyze(ingredient);
                var add = recipe.AddItem(section.Id, analysis.CleanName, quantity: null, comment: null);
                if (add.IsSuccess)
                {
                    routed.Add((add.Value, analysis));
                }
            }
            recipe.AddLink(request.Url, imported.SourceName);
            await db.SaveChangesAsync(ct);

            // Bulk add carries the same obligation as the item-create slice: route each new item so
            // quantity extraction runs. Recipe items never chain product classification (MVP).
            foreach (var (item, analysis) in routed)
            {
                quantityTrigger.OnItemRouted(householdId, recipe.Id, item.Id, analysis);
            }

            // Best-effort cover: pull the JSON-LD image through the same guarded fetch path, re-encode,
            // and attach it (becomes coverAttachmentId). Never fails the import — see TryAttachCoverAsync.
            if (!string.IsNullOrWhiteSpace(imported.ImageUrl))
            {
                await TryAttachCoverAsync(db, importService, imageProcessor, coverStorage, recipe,
                    imported.ImageUrl, loggerFactory.CreateLogger(typeof(ImportRecipeEndpoint)), ct);
            }

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipe.Id}",
                RecipeResponse.From(recipe, creator, routed.Count));
        }

        // Fetch → process → store → attach, all best-effort: any failure (bad URL, blocked IP,
        // non-image, undecodable bytes, save error) is swallowed so the import still succeeds without a
        // cover. Orphaned blobs (a write that lands before a later throw) are reclaimed by ReclaimOrphanBlobs.
        private static async Task TryAttachCoverAsync(
            ApplicationDbContext db,
            RecipeImportService importService,
            IImageProcessor imageProcessor,
            IFileStorage storage,
            Recipe recipe,
            string imageUrl,
            ILogger logger,
            CancellationToken ct)
        {
            try
            {
                var fetch = await importService.FetchImageAsync(imageUrl, ct);
                if (fetch.IsFailed)
                {
                    return;
                }

                Result<ProcessedImage> processed;
                await using (var input = new MemoryStream(fetch.Value))
                {
                    processed = await imageProcessor.ProcessAsync(input, ct);
                }
                if (processed.IsFailed)
                {
                    return;
                }

                var storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
                var thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);
                var stored = new StoredFile(
                    storageKey, thumbnailKey, processed.Value.ContentType,
                    CoverFileName(imageUrl), processed.Value.FullResSizeBytes);

                var add = recipe.AddAttachment(caption: null, stored);
                if (add.IsFailed)
                {
                    return;
                }
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cover image import failed for recipe {RecipeId}.", recipe.Id);
            }
        }

        private static string CoverFileName(string imageUrl)
        {
            var name = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ? Path.GetFileName(uri.LocalPath) : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "cover";
            }
            return name.Length > RecipeAttachment.OriginalFileNameMaxLength
                ? name[..RecipeAttachment.OriginalFileNameMaxLength]
                : name;
        }
    }
}
