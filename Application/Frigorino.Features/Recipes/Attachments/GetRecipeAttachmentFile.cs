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
    public static class GetRecipeAttachmentFileEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeAttachmentFile(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{attachmentId:int}/file", Handle)
               .WithName("GetRecipeAttachmentFile")
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
                .Select(a => new { a.StorageKey, a.ContentType, a.OriginalFileName })
                .FirstOrDefaultAsync(ct);
            if (attachment is null || string.IsNullOrEmpty(attachment.StorageKey)) return TypedResults.NotFound();

            var stream = await storage.OpenAsync(attachment.StorageKey, ct);
            if (stream is null) return TypedResults.NotFound();

            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(
                stream,
                attachment.ContentType ?? "application/octet-stream",
                fileDownloadName: SanitizeFileName(attachment.OriginalFileName));
        }

        private static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '/' && c != '\\' && c != '"').ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }
    }
}
