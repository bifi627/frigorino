using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> builder)
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedOnAdd();
            builder.Property(r => r.Name).HasMaxLength(Recipe.NameMaxLength).IsRequired();
            builder.Property(r => r.Description).HasMaxLength(Recipe.DescriptionMaxLength);
            builder.Property(r => r.Servings);
            builder.Property(r => r.HouseholdId).IsRequired();
            builder.Property(r => r.CreatedByUserId).HasMaxLength(128).IsRequired();
            builder.Property(r => r.CreatedAt).IsRequired();
            builder.Property(r => r.UpdatedAt).IsRequired();
            builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK to Household so the DeleteInactiveItems hard-delete of a household cascades to its
            // recipes. InventoryConfiguration relies on convention (Household.Inventories nav), but
            // Household has no Recipes nav, so we declare the FK explicitly with Cascade to get the
            // same behavior.
            builder.HasOne(r => r.Household)
                .WithMany()
                .HasForeignKey(r => r.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => r.HouseholdId);
            builder.HasIndex(r => r.CreatedByUserId);
            builder.HasIndex(r => r.IsActive);
            builder.HasIndex(r => r.CreatedAt);
            builder.HasIndex(r => new { r.HouseholdId, r.IsActive });
        }
    }
}
