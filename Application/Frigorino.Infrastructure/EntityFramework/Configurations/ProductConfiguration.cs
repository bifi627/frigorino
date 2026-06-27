using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Id)
                .ValueGeneratedOnAdd();

            builder.Property(p => p.HouseholdId)
                .IsRequired();

            builder.Property(p => p.NormalizedName)
                .HasMaxLength(Product.NormalizedNameMaxLength)
                .IsRequired();

            builder.Property(p => p.ClassificationProductCategory)
                .IsRequired();

            builder.Property(p => p.ClassificationExpiryHandling)
                .IsRequired();

            builder.Property(p => p.ClassificationShelfLifeDays);

            builder.Property(p => p.ClassifierVersion)
                .IsRequired();

            builder.Property(p => p.OverrideProductCategory);
            builder.Property(p => p.OverrideExpiryHandling);
            builder.Property(p => p.OverrideShelfLifeDays);

            builder.Property(p => p.CreatedAt)
                .IsRequired();

            builder.Property(p => p.UpdatedAt)
                .IsRequired();

            // One catalog row per (household, normalized name) — the point-lookup key and the
            // arbiter of the concurrent-insert race.
            builder.HasIndex(p => new { p.HouseholdId, p.NormalizedName })
                .IsUnique();

            // FK-only relationship (no navigation added to Household) — cascade with the household.
            builder.HasOne(p => p.Household)
                .WithMany()
                .HasForeignKey(p => p.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.HouseholdId);
        }
    }
}
