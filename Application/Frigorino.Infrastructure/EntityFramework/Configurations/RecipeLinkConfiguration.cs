using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeLinkConfiguration : IEntityTypeConfiguration<RecipeLink>
    {
        public void Configure(EntityTypeBuilder<RecipeLink> builder)
        {
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedOnAdd();
            builder.Property(l => l.Url).HasMaxLength(RecipeLink.UrlMaxLength).IsRequired();
            builder.Property(l => l.Label).HasMaxLength(RecipeLink.LabelMaxLength);
            builder.Property(l => l.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(l => l.RecipeId).IsRequired();
            builder.Property(l => l.CreatedAt).IsRequired();
            builder.Property(l => l.UpdatedAt).IsRequired();
            builder.Property(l => l.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(l => l.Recipe)
                .WithMany(r => r.Links)
                .HasForeignKey(l => l.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(l => l.RecipeId);
            builder.HasIndex(l => l.IsActive);
            builder.HasIndex(l => new { l.RecipeId, l.IsActive });
            builder.HasIndex(l => new { l.RecipeId, l.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeLinks_RecipeId_Rank_Active");
        }
    }
}
