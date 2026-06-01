using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
    {
        public void Configure(EntityTypeBuilder<UserSettings> builder)
        {
            builder.HasKey(s => s.UserId);

            builder.Property(s => s.UserId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(s => s.Language)
                .HasMaxLength(8);

            builder.Property(s => s.ExpiryNotificationsEnabled)
                .HasDefaultValue(false);

            builder.Property(s => s.ExpiryLeadDays)
                .HasDefaultValue(UserSettings.DefaultExpiryLeadDays);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            // 1:1 with User, no navigation on the principal side. Cascade so deleting a user
            // removes their settings row.
            builder.HasOne(s => s.User)
                .WithOne()
                .HasForeignKey<UserSettings>(s => s.UserId)
                .HasPrincipalKey<User>(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
