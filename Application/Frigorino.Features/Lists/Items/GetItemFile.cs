using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class GetItemFileEndpoint
    {
        public static IEndpointRouteBuilder MapGetItemFile(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}/file", Handle)
               .WithName("GetItemFile")
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound>> Handle(
            int householdId,
            int listId,
            int itemId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IFileStorage storage,
            HttpContext http,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var item = await db.ListItems
                .Where(i => i.Id == itemId && i.ListId == listId && i.IsActive
                    && i.List.HouseholdId == householdId && i.List.IsActive)
                .Select(i => new { i.StorageKey, i.ContentType, i.OriginalFileName })
                .FirstOrDefaultAsync(ct);
            if (item is null || string.IsNullOrEmpty(item.StorageKey))
            {
                return TypedResults.NotFound();
            }

            var stream = await storage.OpenAsync(item.StorageKey, ct);
            if (stream is null)
            {
                return TypedResults.NotFound();
            }

            // Content-addressable GUID keys never change → cache hard.
            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(
                stream,
                item.ContentType ?? "application/octet-stream",
                fileDownloadName: SanitizeFileName(item.OriginalFileName));
        }

        // Strip path separators / control chars so OriginalFileName can't inject into Content-Disposition.
        private static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '/' && c != '\\' && c != '"').ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }
    }
}
