using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
    {
        public void Configure(EntityTypeBuilder<Inventory> builder)
        {
            builder.HasKey(i => i.Id);

            builder.Property(i => i.Id)
                .ValueGeneratedOnAdd();

            builder.Property(i => i.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(i => i.Description)
                .HasMaxLength(1000);

            builder.Property(i => i.HouseholdId)
                .IsRequired();

            builder.Property(i => i.CreatedByUserId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(i => i.CreatedAt)
                .IsRequired();

            builder.Property(i => i.UpdatedAt)
                .IsRequired();

            builder.Property(i => i.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships
            builder.HasOne(i => i.CreatedByUser)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(i => i.HouseholdId);
            builder.HasIndex(i => i.CreatedByUserId);
            builder.HasIndex(i => i.IsActive);
            builder.HasIndex(i => i.CreatedAt);
            builder.HasIndex(i => new { i.HouseholdId, i.IsActive });
        }
    }
}
