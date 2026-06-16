using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Files;
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class RecipeAttachmentAggregateTests
    {
        private static Recipe NewRecipe()
        {
            var r = Recipe.Create("Apple Pie", null, householdId: 1, createdByUserId: "u1");
            Assert.True(r.IsSuccess);
            var recipe = r.Value;
            recipe.Id = 10;
            return recipe;
        }

        // Valid stored image: webp output + thumbnail present (what the processor produces).
        private static StoredFile ValidFile() =>
            new("full-key", "thumb-key", "image/webp", "photo.png", 2048);

        // Valid stored document: a PDF with no thumbnail (documents never have one).
        private static StoredFile ValidDocFile() =>
            new("doc-key", null, "application/pdf", "recipe-card.pdf", 4096);

        private static bool HasProperty(FluentResults.IResultBase result, string property) =>
            result.Errors.Any(e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == property);

        [Fact]
        public void AddAttachment_ValidImage_SetsColumnsAndAppends()
        {
            var recipe = NewRecipe();

            var result = recipe.AddAttachment("front of dish", ValidFile());

            Assert.True(result.IsSuccess);
            var a = result.Value;
            Assert.Equal("full-key", a.StorageKey);
            Assert.Equal("thumb-key", a.ThumbnailStorageKey);
            Assert.Equal("image/webp", a.ContentType);
            Assert.Equal("photo.png", a.OriginalFileName);
            Assert.Equal(2048, a.FileSizeBytes);
            Assert.Equal("front of dish", a.Caption);
            Assert.True(a.IsActive);
            Assert.NotEmpty(a.Rank);
            Assert.Single(recipe.Attachments);
        }

        [Fact]
        public void AddAttachment_TrimsCaption_EmptyToNull()
        {
            var recipe = NewRecipe();
            Assert.Equal("hi", recipe.AddAttachment("  hi  ", ValidFile()).Value.Caption);
            Assert.Null(recipe.AddAttachment("   ", ValidFile()).Value.Caption);
        }

        [Fact]
        public void AddAttachment_MissingThumbnail_FailsKeyedOnThumbnail()
        {
            var recipe = NewRecipe();
            var noThumb = new StoredFile("full-key", null, "image/webp", "photo.png", 2048);

            var result = recipe.AddAttachment(null, noThumb);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ThumbnailStorageKey)));
        }

        [Fact]
        public void AddAttachment_WrongStoredContentType_FailsKeyedOnContentType()
        {
            var recipe = NewRecipe();
            var jpeg = new StoredFile("full-key", "thumb-key", "image/jpeg", "photo.jpg", 2048);

            var result = recipe.AddAttachment(null, jpeg);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ContentType)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(RecipeAttachment.MaxFileSizeBytes + 1)]
        public void AddAttachment_BadSize_FailsKeyedOnFileSize(long size)
        {
            var recipe = NewRecipe();
            var bad = new StoredFile("full-key", "thumb-key", "image/webp", "photo.png", size);

            var result = recipe.AddAttachment(null, bad);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.FileSizeBytes)));
        }

        [Fact]
        public void AddAttachment_MissingStorageKey_FailsKeyedOnStorageKey()
        {
            var recipe = NewRecipe();
            var noKey = new StoredFile("   ", "thumb-key", "image/webp", "photo.png", 2048);

            var result = recipe.AddAttachment(null, noKey);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.StorageKey)));
        }

        [Fact]
        public void AddAttachment_CaptionTooLong_FailsKeyedOnCaption()
        {
            var recipe = NewRecipe();
            var caption = new string('x', RecipeAttachment.CaptionMaxLength + 1);

            var result = recipe.AddAttachment(caption, ValidFile());

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.Caption)));
        }

        [Fact]
        public void UpdateAttachmentCaption_ChangesCaptionOnly()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment("old", ValidFile()).Value;
            a.Id = 1;

            var result = recipe.UpdateAttachmentCaption(1, "  new  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("new", recipe.Attachments.Single().Caption);
        }

        [Fact]
        public void UpdateAttachmentCaption_NotFound_Fails()
        {
            var recipe = NewRecipe();
            var result = recipe.UpdateAttachmentCaption(999, "x");
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveThenRestoreAttachment_RoundTrips()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment(null, ValidFile()).Value;
            a.Id = 1;

            Assert.True(recipe.RemoveAttachment(1).IsSuccess);
            Assert.False(recipe.Attachments.Single().IsActive);

            Assert.True(recipe.RestoreAttachment(1).IsSuccess);
            Assert.True(recipe.Attachments.Single().IsActive);
        }

        [Fact]
        public void RestoreAttachment_RankCollision_DeCollides()
        {
            var recipe = NewRecipe();
            var first = recipe.AddAttachment(null, ValidFile()).Value;
            first.Id = 1;
            recipe.RemoveAttachment(1);
            // A new attachment takes the freed-up rank slot while #1 is deleted.
            var second = recipe.AddAttachment(null, ValidFile()).Value;
            second.Id = 2;
            second.Rank = first.Rank; // force the collision

            var result = recipe.RestoreAttachment(1);

            Assert.True(result.IsSuccess);
            var ranks = recipe.Attachments.Where(x => x.IsActive).Select(x => x.Rank).ToList();
            Assert.Equal(ranks.Count, ranks.Distinct().Count()); // no two active rows share a rank
        }

        [Fact]
        public void ReorderAttachment_ToTop_PlacesFirst()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment(null, ValidFile()).Value; a.Id = 1;
            var b = recipe.AddAttachment(null, ValidFile()).Value; b.Id = 2;

            var result = recipe.ReorderAttachment(2, afterAttachmentId: 0);

            Assert.True(result.IsSuccess);
            var ordered = recipe.Attachments.Where(x => x.IsActive)
                .OrderBy(x => x.Rank, StringComparer.Ordinal).Select(x => x.Id).ToList();
            Assert.Equal(2, ordered[0]);
        }

        [Fact]
        public void AddAttachment_ValidImage_StampsImageType()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment(null, ValidFile()).Value;
            Assert.Equal(AttachmentType.Image, a.Type);
        }

        [Fact]
        public void AddDocumentAttachment_ValidPdf_SetsColumnsAndType()
        {
            var recipe = NewRecipe();

            var result = recipe.AddDocumentAttachment("scan", ValidDocFile());

            Assert.True(result.IsSuccess);
            var a = result.Value;
            Assert.Equal(AttachmentType.Document, a.Type);
            Assert.Equal("doc-key", a.StorageKey);
            Assert.Null(a.ThumbnailStorageKey);
            Assert.Equal("application/pdf", a.ContentType);
            Assert.Equal("recipe-card.pdf", a.OriginalFileName);
            Assert.Equal(4096, a.FileSizeBytes);
            Assert.Equal("scan", a.Caption);
            Assert.True(a.IsActive);
            Assert.NotEmpty(a.Rank);
            Assert.Single(recipe.Attachments);
        }

        [Fact]
        public void AddDocumentAttachment_WrongContentType_FailsKeyedOnContentType()
        {
            var recipe = NewRecipe();
            var notPdf = new StoredFile("doc-key", null, "image/webp", "x.webp", 4096);

            var result = recipe.AddDocumentAttachment(null, notPdf);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ContentType)));
        }

        [Fact]
        public void AddDocumentAttachment_WithThumbnail_FailsKeyedOnThumbnail()
        {
            var recipe = NewRecipe();
            var withThumb = new StoredFile("doc-key", "thumb-key", "application/pdf", "x.pdf", 4096);

            var result = recipe.AddDocumentAttachment(null, withThumb);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ThumbnailStorageKey)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(RecipeAttachment.MaxFileSizeBytes + 1)]
        public void AddDocumentAttachment_BadSize_FailsKeyedOnFileSize(long size)
        {
            var recipe = NewRecipe();
            var bad = new StoredFile("doc-key", null, "application/pdf", "x.pdf", size);

            var result = recipe.AddDocumentAttachment(null, bad);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.FileSizeBytes)));
        }

        [Fact]
        public void AddDocumentAttachment_MissingStorageKey_FailsKeyedOnStorageKey()
        {
            var recipe = NewRecipe();
            var noKey = new StoredFile("  ", null, "application/pdf", "x.pdf", 4096);

            var result = recipe.AddDocumentAttachment(null, noKey);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.StorageKey)));
        }

        [Fact]
        public void AddDocumentAttachment_CaptionTooLong_FailsKeyedOnCaption()
        {
            var recipe = NewRecipe();
            var caption = new string('x', RecipeAttachment.CaptionMaxLength + 1);

            var result = recipe.AddDocumentAttachment(caption, ValidDocFile());

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.Caption)));
        }
    }
}
