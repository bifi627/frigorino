using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class SortBlueprintCategoryConfiguration : IEntityTypeConfiguration<SortBlueprintCategory>
    {
        public void Configure(EntityTypeBuilder<SortBlueprintCategory> builder)
        {
            // Composite key — an aisle appears at most once per blueprint. Category stored as int.
            builder.HasKey(c => new { c.BlueprintId, c.Category });

            builder.Property(c => c.Category).IsRequired();
            builder.Property(c => c.OrderIndex).IsRequired();
        }
    }
}
