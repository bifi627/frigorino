using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeSectionConfiguration : IEntityTypeConfiguration<RecipeSection>
    {
        public void Configure(EntityTypeBuilder<RecipeSection> builder)
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedOnAdd();
            builder.Property(s => s.Name).HasMaxLength(RecipeSection.NameMaxLength);
            builder.Property(s => s.Description).HasMaxLength(RecipeSection.DescriptionMaxLength);
            builder.Property(s => s.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(s => s.RecipeId).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();
            builder.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(s => s.Recipe)
                .WithMany(r => r.Sections)
                .HasForeignKey(s => s.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => s.RecipeId);
            builder.HasIndex(s => s.IsActive);
            builder.HasIndex(s => new { s.RecipeId, s.IsActive });
            builder.HasIndex(s => new { s.RecipeId, s.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeSections_RecipeId_Rank_Active");
        }
    }
}
