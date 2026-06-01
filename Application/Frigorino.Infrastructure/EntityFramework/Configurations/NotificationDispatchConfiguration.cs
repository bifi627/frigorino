using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
    {
        public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
        {
            builder.HasKey(d => d.Id);

            builder.Property(d => d.UserId)
                .HasMaxLength(128)
                .IsRequired();

            // At most one digest per user-household-day.
            builder.HasIndex(d => new { d.UserId, d.HouseholdId, d.SentOn }).IsUnique();
        }
    }
}
