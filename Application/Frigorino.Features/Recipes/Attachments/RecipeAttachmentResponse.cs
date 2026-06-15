using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes.Attachments
{
    // Storage keys are deliberately NOT exposed — the client fetches /file and /thumbnail.
    public sealed record RecipeAttachmentResponse(
        int Id,
        int RecipeId,
        string ContentType,
        string? OriginalFileName,
        long FileSizeBytes,
        string? Caption,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static RecipeAttachmentResponse From(RecipeAttachment a)
            => new(a.Id, a.RecipeId, a.ContentType, a.OriginalFileName, a.FileSizeBytes, a.Caption, a.Rank, a.CreatedAt, a.UpdatedAt);

        public static readonly Expression<Func<RecipeAttachment, RecipeAttachmentResponse>> ToProjection = a =>
            new RecipeAttachmentResponse(a.Id, a.RecipeId, a.ContentType, a.OriginalFileName, a.FileSizeBytes, a.Caption, a.Rank, a.CreatedAt, a.UpdatedAt);
    }
}
