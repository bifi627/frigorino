using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class SortBlueprintConfiguration : IEntityTypeConfiguration<SortBlueprint>
    {
        public void Configure(EntityTypeBuilder<SortBlueprint> builder)
        {
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id)
                .ValueGeneratedOnAdd();

            builder.Property(b => b.HouseholdId)
                .IsRequired();

            builder.Property(b => b.Name)
                .HasMaxLength(SortBlueprint.NameMaxLength)
                .IsRequired();

            builder.Property(b => b.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(b => b.CreatedAt).IsRequired();
            builder.Property(b => b.UpdatedAt).IsRequired();

            // FK-only relationship to Household — cascade with the household.
            builder.HasOne(b => b.Household)
                .WithMany()
                .HasForeignKey(b => b.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Children deleted with the blueprint (and orphan-deleted on wholesale replace).
            builder.HasMany(b => b.Categories)
                .WithOne(c => c.Blueprint)
                .HasForeignKey(c => c.BlueprintId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(b => new { b.HouseholdId, b.IsActive });
        }
    }
}
