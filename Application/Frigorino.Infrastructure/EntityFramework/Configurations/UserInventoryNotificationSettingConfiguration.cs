using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class UserInventoryNotificationSettingConfiguration : IEntityTypeConfiguration<UserInventoryNotificationSetting>
    {
        public void Configure(EntityTypeBuilder<UserInventoryNotificationSetting> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.UserId)
                .HasMaxLength(128)
                .IsRequired();

            // ValueGeneratedNever: always send the explicit CLR value on INSERT so the lazy-create
            // path can't lose a default-false (mute) to the OnAdd sentinel-skip (same pattern as the
            // old InventorySettings.ExpiryNotificationsEnabled config).
            builder.Property(s => s.Enabled)
                .HasDefaultValue(true)
                .ValueGeneratedNever();

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            // One preference row per (user, inventory).
            builder.HasIndex(s => new { s.UserId, s.InventoryId }).IsUnique();

            builder.HasOne(s => s.Inventory)
                .WithMany()
                .HasForeignKey(s => s.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
