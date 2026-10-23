using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Administration;

namespace Situ.Infrastructure.Persistence.Configurations.Administration;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.Property(e => e.Key).HasMaxLength(100);
        builder.Property(e => e.Value).HasMaxLength(1000);
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.HasIndex(e => e.Key).IsUnique().AreNullsDistinct(false);
    }
}
