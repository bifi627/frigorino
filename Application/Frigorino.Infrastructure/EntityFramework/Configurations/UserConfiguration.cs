using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.ExternalId);

            builder.Property(u => u.ExternalId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(u => u.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(u => u.Email)
                .HasMaxLength(320)
                .IsRequired();

            builder.Property(u => u.CreatedAt)
                .IsRequired();

            builder.Property(u => u.LastLoginAt)
                .IsRequired();

            builder.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships
            builder.HasMany(u => u.UserHouseholds)
                .WithOne(uh => uh.User)
                .HasForeignKey(uh => uh.UserId)
                .HasPrincipalKey(u => u.ExternalId);

            builder.HasMany(u => u.CreatedHouseholds)
                .WithOne(h => h.CreatedByUser)
                .HasForeignKey(h => h.CreatedByUserId)
                .HasPrincipalKey(u => u.ExternalId);

            // Indexes
            builder.HasIndex(u => u.Email)
                .IsUnique();

            builder.HasIndex(u => u.IsActive);
        }
    }
}
