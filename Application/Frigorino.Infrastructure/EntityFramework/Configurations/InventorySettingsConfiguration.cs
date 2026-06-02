using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class InventorySettingsConfiguration : IEntityTypeConfiguration<InventorySettings>
    {
        public void Configure(EntityTypeBuilder<InventorySettings> builder)
        {
            builder.HasKey(s => s.InventoryId);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            builder.HasOne(s => s.Inventory)
                .WithOne()
                .HasForeignKey<InventorySettings>(s => s.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
