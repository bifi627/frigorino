using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeAttachmentConfiguration : IEntityTypeConfiguration<RecipeAttachment>
    {
        public void Configure(EntityTypeBuilder<RecipeAttachment> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedOnAdd();
            builder.Property(a => a.StorageKey).HasMaxLength(RecipeAttachment.StorageKeyMaxLength).IsRequired();
            builder.Property(a => a.ThumbnailStorageKey).HasMaxLength(RecipeAttachment.StorageKeyMaxLength);
            builder.Property(a => a.ContentType).HasMaxLength(RecipeAttachment.ContentTypeMaxLength).IsRequired();
            builder.Property(a => a.OriginalFileName).HasMaxLength(RecipeAttachment.OriginalFileNameMaxLength);
            builder.Property(a => a.FileSizeBytes).IsRequired();
            builder.Property(a => a.Caption).HasMaxLength(RecipeAttachment.CaptionMaxLength);
            builder.Property(a => a.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(a => a.RecipeId).IsRequired();
            builder.Property(a => a.CreatedAt).IsRequired();
            builder.Property(a => a.UpdatedAt).IsRequired();
            builder.Property(a => a.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(a => a.Recipe)
                .WithMany(r => r.Attachments)
                .HasForeignKey(a => a.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => a.RecipeId);
            builder.HasIndex(a => a.IsActive);
            builder.HasIndex(a => new { a.RecipeId, a.IsActive });
            builder.HasIndex(a => new { a.RecipeId, a.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeAttachments_RecipeId_Rank_Active");
        }
    }
}
