using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class FcmTokenConfiguration : IEntityTypeConfiguration<FcmToken>
    {
        public void Configure(EntityTypeBuilder<FcmToken> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.UserId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(t => t.Token)
                .HasMaxLength(512)
                .IsRequired();

            builder.Property(t => t.CreatedAt).IsRequired();
            builder.Property(t => t.LastSeenAt).IsRequired();

            // A device token is globally unique; re-registration reassigns the owner.
            builder.HasIndex(t => t.Token).IsUnique();

            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
