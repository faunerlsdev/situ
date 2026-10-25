using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.Property(e => e.NotifyNearbyReports).HasDefaultValue(true);
        builder.Property(e => e.TimeZoneId).HasMaxLength(64).IsRequired().HasDefaultValue(NotificationSettings.DefaultTimeZoneId);
        builder.Required<NotificationSettings, User>(e => e.UserId);
        builder.HasIndex(e => e.UserId).IsUnique().AreNullsDistinct(false);
    }
}
