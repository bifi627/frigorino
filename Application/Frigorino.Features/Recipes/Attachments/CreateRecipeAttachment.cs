using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class CreateRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeAttachment")
               .DisableAntiforgery() // API endpoint: no antiforgery token on multipart form posts.
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status413PayloadTooLarge)
               .ProducesValidationProblem();
            return app;
        }

        public static async Task<Results<Created<RecipeAttachmentResponse>, NotFound, ValidationProblem, StatusCodeHttpResult>> Handle(
            int householdId,
            int recipeId,
            IFormFile file,
            [FromForm] string? caption,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage storage,
            IImageProcessor imageProcessor,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger(typeof(CreateRecipeAttachmentEndpoint));

            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            if (file is null || file.Length <= 0)
            {
                return new Error("A file is required.").WithProperty("file").ToValidationProblemResult();
            }

            // App-level size gate — the real limit; framework defaults are only the outer backstop.
            if (file.Length > RecipeAttachment.MaxFileSizeBytes)
            {
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            // Input content-type allowlist pre-filter: route to the image or document path, else 400.
            var isImage = RecipeAttachment.ImageContentTypes.Contains(file.ContentType);
            var isDocument = RecipeAttachment.DocumentContentTypes.Contains(file.ContentType);
            if (!isImage && !isDocument)
            {
                return new Error($"Content type '{file.ContentType}' is not an allowed type.")
                    .WithProperty("file").ToValidationProblemResult();
            }

            // Orphan-safe ordering: save blobs BEFORE the row exists; compensate on any later failure.
            string? storageKey = null;
            string? thumbnailKey = null;
            try
            {
                StoredFile stored;
                Func<Recipe, Result<RecipeAttachment>> addToRecipe;

                if (isImage)
                {
                    Result<ProcessedImage> processed;
                    await using (var upload = file.OpenReadStream())
                    {
                        processed = await imageProcessor.ProcessAsync(upload, ct);
                    }
                    if (processed.IsFailed) return processed.ToValidationProblem();

                    storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
                    thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);
                    stored = new StoredFile(
                        storageKey, thumbnailKey, processed.Value.ContentType,
                        file.FileName, processed.Value.FullResSizeBytes);
                    addToRecipe = recipe => recipe.AddAttachment(caption, stored);
                }
                else
                {
                    // Document path: store the raw PDF bytes, no processing, no thumbnail.
                    await using (var upload = file.OpenReadStream())
                    {
                        storageKey = await storage.SaveAsync(upload, ct);
                    }
                    stored = new StoredFile(storageKey, null, file.ContentType, file.FileName, file.Length);
                    addToRecipe = recipe => recipe.AddDocumentAttachment(caption, stored);
                }

                var outcome = await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var recipe = await db.Recipes
                        .Include(r => r.Attachments)
                        .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                    if (recipe is null)
                    {
                        return new CreateOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = addToRecipe(recipe);
                    if (result.IsFailed)
                    {
                        return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);
                    return new CreateOutcome(RecipeAttachmentResponse.From(result.Value), NotFound: false, Problem: null);
                });

                if (outcome.NotFound)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                    return TypedResults.NotFound();
                }
                if (outcome.Problem is not null)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                    return outcome.Problem;
                }

                var response = outcome.Response!;
                return TypedResults.Created(
                    $"/api/household/{householdId}/recipes/{recipeId}/attachments/{response.Id}", response);
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                throw;
            }
        }

        private sealed record CreateOutcome(RecipeAttachmentResponse? Response, bool NotFound, ValidationProblem? Problem);

        private static async Task CompensateAsync(IFileStorage storage, string? storageKey, string? thumbnailKey, ILogger logger)
        {
            await DeleteQuietlyAsync(storage, storageKey, logger);
            await DeleteQuietlyAsync(storage, thumbnailKey, logger);
        }

        private static async Task DeleteQuietlyAsync(IFileStorage storage, string? key, ILogger logger)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete orphaned blob {Key} during attachment-upload compensation.", key);
            }
        }
    }
}
