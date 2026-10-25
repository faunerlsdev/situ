using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Notifications;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class NotificationJobConfiguration : IEntityTypeConfiguration<NotificationJob>
{
    public void Configure(EntityTypeBuilder<NotificationJob> builder)
    {
        builder.Property(e => e.Kind).HasMaxLength(40);
        builder.Property(e => e.LastError).HasMaxLength(300);
        builder.HasIndex(e => new { e.Kind, e.SubjectId }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(e => new { e.Status, e.AvailableAt });
    }
}
