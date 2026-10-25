using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class DeviceAlertLocationConfiguration : IEntityTypeConfiguration<DeviceAlertLocation>
{
    public void Configure(EntityTypeBuilder<DeviceAlertLocation> builder)
    {
        builder.Required<DeviceAlertLocation, User>(e => e.UserId);
        builder.Required<DeviceAlertLocation, UserDevice>(e => e.DeviceId);
        builder.HasIndex(e => e.DeviceId).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(e => e.ExpiresAt);
    }
}
