using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class GetRecipeAttachmentThumbnailEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeAttachmentThumbnail(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{attachmentId:int}/thumbnail", Handle)
               .WithName("GetRecipeAttachmentThumbnail")
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage storage,
            HttpContext http,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var attachment = await db.RecipeAttachments
                .Where(a => a.Id == attachmentId && a.RecipeId == recipeId && a.IsActive
                    && a.Recipe.HouseholdId == householdId && a.Recipe.IsActive)
                .Select(a => new { a.ThumbnailStorageKey, a.ContentType })
                .FirstOrDefaultAsync(ct);
            if (attachment is null || string.IsNullOrEmpty(attachment.ThumbnailStorageKey)) return TypedResults.NotFound();

            var stream = await storage.OpenAsync(attachment.ThumbnailStorageKey, ct);
            if (stream is null) return TypedResults.NotFound();

            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(stream, attachment.ContentType ?? "application/octet-stream");
        }
    }
}
