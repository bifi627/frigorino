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

namespace Frigorino.Features.Lists.Items
{
    public static class CreateMediaItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateMediaItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("/media", Handle)
               .WithName("CreateMediaItem")
               .DisableAntiforgery() // API endpoint: no antiforgery token on multipart form posts.
               .Produces<ListItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status413PayloadTooLarge)
               .ProducesValidationProblem();
            return app;
        }

        public static async Task<Results<Created<ListItemResponse>, NotFound, ValidationProblem, StatusCodeHttpResult>> Handle(
            int householdId,
            int listId,
            IFormFile file,
            [FromForm] ListItemType type,
            [FromForm] string? caption,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.ListItem)] IFileStorage storage,
            IImageProcessor imageProcessor,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger(typeof(CreateMediaItemEndpoint));

            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            if (file is null || file.Length <= 0)
            {
                return new Error("A file is required.").WithProperty("file").ToValidationProblemResult();
            }

            // App-level size gate — the real limit; framework defaults (Kestrel/multipart) are only the outer backstop.
            if (file.Length > ListItem.MaxFileSizeBytes)
            {
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            Result<ProcessedImage> processed;
            await using (var upload = file.OpenReadStream())
            {
                processed = await imageProcessor.ProcessAsync(upload, ct);
            }
            if (processed.IsFailed)
            {
                return processed.ToValidationProblem();
            }

            // Orphan-safe ordering: save blobs BEFORE the row exists; compensate on any later failure.
            // Both saves live inside the try so a throw on the SECOND save still compensates the first.
            string? storageKey = null;
            string? thumbnailKey = null;
            try
            {
                storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
                thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);

                var stored = new StoredFile(
                    storageKey, thumbnailKey, processed.Value.ContentType,
                    file.FileName, processed.Value.FullResSizeBytes);

                // AddMediaItem mints a Rank (append after the last unchecked item); a concurrent append
                // can collide on the partial unique index. RankRetry reloads fresh state and re-mints —
                // only the EF insert is retried; the blobs are already stored above and not re-uploaded.
                var outcome = await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var freshList = await db.Lists
                        .Include(l => l.ListItems)
                        .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                    if (freshList is null)
                    {
                        return new MediaOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = freshList.AddMediaItem(type, caption, stored);
                    if (result.IsFailed)
                    {
                        return new MediaOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);
                    return new MediaOutcome(ListItemResponse.From(result.Value), NotFound: false, Problem: null);
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
                    $"/api/household/{householdId}/lists/{listId}/items/{response.Id}",
                    response);
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                throw;
            }
        }

        private sealed record MediaOutcome(ListItemResponse? Response, bool NotFound, ValidationProblem? Problem);

        // Best-effort cleanup of just-uploaded blobs; deletes each key independently so one failure can't
        // orphan the other blob or mask the original error. Null/empty keys (save never happened) are skipped.
        private static async Task CompensateAsync(
            IFileStorage storage, string? storageKey, string? thumbnailKey, ILogger logger)
        {
            await DeleteQuietlyAsync(storage, storageKey, logger);
            await DeleteQuietlyAsync(storage, thumbnailKey, logger);
        }

        private static async Task DeleteQuietlyAsync(IFileStorage storage, string? key, ILogger logger)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete orphaned blob {Key} during media-upload compensation.", key);
            }
        }
    }
}
