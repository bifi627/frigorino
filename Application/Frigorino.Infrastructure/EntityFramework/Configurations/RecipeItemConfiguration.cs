using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
    {
        public void Configure(EntityTypeBuilder<RecipeItem> builder)
        {
            builder.HasKey(ri => ri.Id);
            builder.Property(ri => ri.Id).ValueGeneratedOnAdd();
            builder.Property(ri => ri.Text).HasMaxLength(RecipeItem.TextMaxLength).IsRequired();
            builder.Property(ri => ri.Comment).HasMaxLength(RecipeItem.CommentMaxLength);
            builder.Property(ri => ri.QuantityValue).HasColumnType("numeric(12,3)");
            builder.Property(ri => ri.QuantityUnit);
            builder.Property(ri => ri.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(ri => ri.RecipeId).IsRequired();
            builder.Property(ri => ri.CreatedAt).IsRequired();
            builder.Property(ri => ri.UpdatedAt).IsRequired();
            builder.Property(ri => ri.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(ri => ri.Recipe)
                .WithMany(r => r.Items)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(ri => ri.RecipeId);
            builder.HasIndex(ri => ri.IsActive);
            builder.HasIndex(ri => ri.CreatedAt);
            builder.HasIndex(ri => new { ri.RecipeId, ri.IsActive });
            builder.HasIndex(ri => new { ri.RecipeId, ri.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeItems_RecipeId_Rank_Active");
        }
    }
}
