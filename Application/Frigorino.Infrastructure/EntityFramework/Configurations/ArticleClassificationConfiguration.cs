using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class ArticleClassificationConfiguration : IEntityTypeConfiguration<ArticleClassification>
    {
        public void Configure(EntityTypeBuilder<ArticleClassification> builder)
        {
            builder.HasKey(ac => ac.Id);
            builder.HasIndex(ac => ac.OriginalName);

            builder.Property(ac => ac.OriginalName).HasMaxLength(500).IsRequired();
        }
    }
}
