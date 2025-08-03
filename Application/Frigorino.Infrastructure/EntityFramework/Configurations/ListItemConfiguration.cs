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
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(li => li.Quantity)
                .HasMaxLength(100);

            builder.Property(li => li.Status)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(li => li.SortOrder)
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
            builder.HasIndex(li => li.SortOrder);
            builder.HasIndex(li => li.Status);
            builder.HasIndex(li => li.IsActive);
            builder.HasIndex(li => new { li.ListId, li.Status, li.SortOrder }); // Composite index for sorting
            builder.HasIndex(li => new { li.ListId, li.IsActive });
        }
    }
}
