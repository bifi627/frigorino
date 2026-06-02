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

            // At most one notification per user-inventory-day.
            builder.HasIndex(d => new { d.UserId, d.InventoryId, d.SentOn }).IsUnique();
        }
    }
}
