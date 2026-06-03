using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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
            IFileStorage storage,
            IImageProcessor imageProcessor,
            CancellationToken ct)
        {
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

            // Transport guard before any expensive work; FormOptions/Kestrel is the hard backstop.
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
            var storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
            var thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);

            try
            {
                var stored = new StoredFile(
                    storageKey, thumbnailKey, processed.Value.ContentType,
                    file.FileName, processed.Value.FullResSizeBytes);

                var result = list.AddMediaItem(type, caption, stored);
                if (result.IsFailed)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey);
                    return result.ToValidationProblem();
                }

                await db.SaveChangesAsync(ct);

                return TypedResults.Created(
                    $"/api/household/{householdId}/lists/{listId}/items/{result.Value.Id}",
                    ListItemResponse.From(result.Value));
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey);
                throw;
            }
        }

        // Best-effort cleanup of just-uploaded blobs; DeleteAsync is idempotent.
        private static async Task CompensateAsync(IFileStorage storage, string storageKey, string thumbnailKey)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            await storage.DeleteAsync(thumbnailKey, CancellationToken.None);
        }
    }
}
