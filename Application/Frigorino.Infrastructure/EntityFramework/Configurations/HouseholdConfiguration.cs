using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
    {
        public void Configure(EntityTypeBuilder<Household> builder)
        {
            builder.HasKey(h => h.Id);
            
            builder.Property(h => h.Id)
                .ValueGeneratedOnAdd();
            
            builder.Property(h => h.Name)
                .HasMaxLength(255)
                .IsRequired();
            
            builder.Property(h => h.Description)
                .HasMaxLength(1000);
            
            builder.Property(h => h.CreatedByUserId)
                .HasMaxLength(128)
                .IsRequired();
            
            builder.Property(h => h.CreatedAt)
                .IsRequired();
            
            builder.Property(h => h.UpdatedAt)
                .IsRequired();
            
            builder.Property(h => h.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships
            builder.HasOne(h => h.CreatedByUser)
                .WithMany(u => u.CreatedHouseholds)
                .HasForeignKey(h => h.CreatedByUserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(h => h.UserHouseholds)
                .WithOne(uh => uh.Household)
                .HasForeignKey(uh => uh.HouseholdId);

            builder.HasMany(h => h.Lists)
                .WithOne(l => l.Household)
                .HasForeignKey(l => l.HouseholdId);

            // Indexes
            builder.HasIndex(h => h.CreatedByUserId);
            builder.HasIndex(h => h.IsActive);
            builder.HasIndex(h => h.CreatedAt);
        }
    }
}
