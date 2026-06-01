using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class HouseholdSettingsConfiguration : IEntityTypeConfiguration<HouseholdSettings>
    {
        public void Configure(EntityTypeBuilder<HouseholdSettings> builder)
        {
            builder.HasKey(s => s.HouseholdId);

            builder.Property(s => s.CheckedItemRetentionDays)
                .IsRequired()
                .HasDefaultValue(HouseholdSettings.DefaultCheckedItemRetentionDays);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            builder.HasOne(s => s.Household)
                .WithOne()
                .HasForeignKey<HouseholdSettings>(s => s.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
