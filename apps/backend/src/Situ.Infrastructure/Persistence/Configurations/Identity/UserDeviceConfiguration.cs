using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.Property(e => e.DeviceIdentifier).HasMaxLength(128);
        builder.Property(e => e.DeviceName).HasMaxLength(100);
        builder.Property(e => e.AppVersion).HasMaxLength(30);
        builder.Property(e => e.PushToken).HasMaxLength(512);
        builder.Property(e => e.LastIpAddress).HasMaxLength(64);
        builder.Required<UserDevice, User>(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.DeviceIdentifier }).IsUnique().AreNullsDistinct(false);
    }
}
