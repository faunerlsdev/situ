using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.Property(e => e.Error).HasMaxLength(500);
        builder.Required<NotificationDelivery, Notification>(e => e.NotificationId);
        builder.Optional<NotificationDelivery, UserDevice>(e => e.DeviceId);
    }
}
