using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class ListItemConfiguration : IEntityTypeConfiguration<ListItem>
    {
        public void Configure(EntityTypeBuilder<ListItem> builder)
        {
            builder.HasKey(li => li.Id);

            builder.Property(li => li.Id)
                .ValueGeneratedOnAdd();

            builder.Property(li => li.ListId)
                .IsRequired();

            builder.Property(li => li.Text)
                .HasMaxLength(ListItem.TextMaxLength)
                .IsRequired();

            builder.Property(li => li.Comment)
                .HasMaxLength(ListItem.CommentMaxLength);

            builder.Property(li => li.Type)
                .IsRequired()
                .HasDefaultValue(ListItemType.Text);

            builder.Property(li => li.StorageKey)
                .HasMaxLength(ListItem.StorageKeyMaxLength);

            builder.Property(li => li.ThumbnailStorageKey)
                .HasMaxLength(ListItem.StorageKeyMaxLength);

            builder.Property(li => li.OriginalFileName)
                .HasMaxLength(ListItem.OriginalFileNameMaxLength);

            builder.Property(li => li.ContentType)
                .HasMaxLength(ListItem.ContentTypeMaxLength);

            builder.Property(li => li.FileSizeBytes);

            // Promotion-to-inventory state. PromotionExpiryHandling is a nullable enum → nullable
            // int column (matches the QuantityUnit convention below).
            builder.Property(li => li.PromotionExpiryHandling);
            builder.Property(li => li.PromotionSuggestedExpiry);
            builder.Property(li => li.PromotionResolvedAt);

            builder.Property(li => li.QuantityValue)
                .HasColumnType("numeric(12,3)");

            // QuantityUnit is a nullable enum — EF maps it to a nullable integer column.
            builder.Property(li => li.QuantityUnit);

            builder.Property(li => li.Status)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(li => li.Rank)
                .HasColumnType("text")
                .UseCollation("C") // byte-ordinal; matches FractionalIndex's ordinal comparison
                .IsRequired();

            builder.Property(li => li.CreatedAt)
                .IsRequired();

            builder.Property(li => li.UpdatedAt)
                .IsRequired();

            builder.Property(li => li.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships
            builder.HasOne(li => li.List)
                .WithMany(l => l.ListItems)
                .HasForeignKey(li => li.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            builder.HasIndex(li => li.ListId);
            builder.HasIndex(li => li.Status);
            builder.HasIndex(li => li.IsActive);
            builder.HasIndex(li => new { li.ListId, li.IsActive });
            // Supports the pending-promotion count (GetList) and the pending-promotions detail read.
            builder.HasIndex(li => new { li.ListId, li.Status, li.PromotionResolvedAt });
            // Ordered fetch + concurrent-reorder collision guard (active rows only).
            builder.HasIndex(li => new { li.ListId, li.Status, li.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_ListItems_ListId_Status_Rank_Active");
        }
    }
}
