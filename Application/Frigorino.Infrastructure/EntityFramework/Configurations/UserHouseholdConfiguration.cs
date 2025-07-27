using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class UserHouseholdConfiguration : IEntityTypeConfiguration<UserHousehold>
    {
        public void Configure(EntityTypeBuilder<UserHousehold> builder)
        {
            // Composite key
            builder.HasKey(uh => new { uh.UserId, uh.HouseholdId });
            
            builder.Property(uh => uh.UserId)
                .HasMaxLength(128)
                .IsRequired();
            
            builder.Property(uh => uh.HouseholdId)
                .IsRequired();
            
            builder.Property(uh => uh.Role)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(HouseholdRole.Member);
            
            builder.Property(uh => uh.JoinedAt)
                .IsRequired();
            
            builder.Property(uh => uh.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships
            builder.HasOne(uh => uh.User)
                .WithMany(u => u.UserHouseholds)
                .HasForeignKey(uh => uh.UserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(uh => uh.Household)
                .WithMany(h => h.UserHouseholds)
                .HasForeignKey(uh => uh.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(uh => uh.UserId);
            builder.HasIndex(uh => uh.HouseholdId);
            builder.HasIndex(uh => uh.Role);
            builder.HasIndex(uh => uh.IsActive);
            builder.HasIndex(uh => uh.JoinedAt);
        }
    }
}
