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
    public static class GetItemThumbnailEndpoint
    {
        public static IEndpointRouteBuilder MapGetItemThumbnail(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}/thumbnail", Handle)
               .WithName("GetItemThumbnail")
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
                .Select(i => new { i.ThumbnailStorageKey, i.ContentType })
                .FirstOrDefaultAsync(ct);
            if (item is null || string.IsNullOrEmpty(item.ThumbnailStorageKey))
            {
                return TypedResults.NotFound();
            }

            var stream = await storage.OpenAsync(item.ThumbnailStorageKey, ct);
            if (stream is null)
            {
                return TypedResults.NotFound();
            }

            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(stream, item.ContentType ?? "application/octet-stream");
        }
    }
}
