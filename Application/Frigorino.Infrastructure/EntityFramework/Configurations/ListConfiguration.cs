using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class ListConfiguration : IEntityTypeConfiguration<List>
    {
        public void Configure(EntityTypeBuilder<List> builder)
        {
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Id)
                .ValueGeneratedOnAdd();

            builder.Property(l => l.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(l => l.Description)
                .HasMaxLength(1000);

            builder.Property(l => l.HouseholdId)
                .IsRequired();

            builder.Property(l => l.CreatedByUserId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(l => l.CreatedAt)
                .IsRequired();

            builder.Property(l => l.UpdatedAt)
                .IsRequired();

            builder.Property(l => l.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure relationships - Remove the Household relationship since it's defined in HouseholdConfiguration
            builder.HasOne(l => l.CreatedByUser)
                .WithMany()
                .HasForeignKey(l => l.CreatedByUserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(l => l.HouseholdId);
            builder.HasIndex(l => l.CreatedByUserId);
            builder.HasIndex(l => l.IsActive);
            builder.HasIndex(l => l.CreatedAt);
            builder.HasIndex(l => new { l.HouseholdId, l.IsActive });
        }
    }
}
