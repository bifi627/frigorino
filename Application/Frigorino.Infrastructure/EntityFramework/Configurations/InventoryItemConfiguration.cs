using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
    {
        public void Configure(EntityTypeBuilder<InventoryItem> builder)
        {
            builder.HasKey(ii => ii.Id);

            builder.Property(ii => ii.Id)
                .ValueGeneratedOnAdd();

            builder.Property(ii => ii.Text)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(ii => ii.Quantity);

            builder.Property(ii => ii.SortOrder)
                .IsRequired();

            builder.Property(ii => ii.ExpiryDate);

            builder.Property(ii => ii.InventoryId)
                .IsRequired();

            builder.Property(ii => ii.CreatedAt)
                .IsRequired();

            builder.Property(ii => ii.UpdatedAt)
                .IsRequired();

            builder.Property(ii => ii.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships
            builder.HasOne(ii => ii.Inventory)
                .WithMany(i => i.InventoryItems)
                .HasForeignKey(ii => ii.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(ii => ii.InventoryId);
            builder.HasIndex(ii => ii.IsActive);
            builder.HasIndex(ii => ii.CreatedAt);
            builder.HasIndex(ii => ii.ExpiryDate);
            builder.HasIndex(ii => ii.SortOrder);
            builder.HasIndex(ii => new { ii.InventoryId, ii.IsActive });
            builder.HasIndex(ii => new { ii.ExpiryDate, ii.IsActive });
            builder.HasIndex(ii => new { ii.InventoryId, ii.SortOrder });
        }
    }
}
