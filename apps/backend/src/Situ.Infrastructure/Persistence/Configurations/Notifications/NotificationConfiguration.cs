using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(120);
        builder.Property(e => e.Body).HasMaxLength(400);
        builder.Property(e => e.GroupKey).HasMaxLength(40);
        builder.Property(e => e.BundleCount).HasDefaultValue(1);
        builder.Required<Notification, User>(e => e.RecipientId);
        builder.HasIndex(e => new { e.RecipientId, e.TargetType, e.TargetId })
            .IsUnique()
            .HasFilter("target_id IS NOT NULL");
        builder.HasIndex(e => new { e.RecipientId, e.GroupKey })
            .IsUnique()
            .HasFilter("group_key IS NOT NULL");
        builder.HasIndex(e => new { e.RecipientId, e.CreatedAt });
    }
}
