using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Administration;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Administration;

public sealed class StaffPermissionConfiguration : IEntityTypeConfiguration<StaffPermission>
{
    public void Configure(EntityTypeBuilder<StaffPermission> builder)
    {
        builder.Property(e => e.Code).HasMaxLength(100);
        builder.Required<StaffPermission, StaffRole>(e => e.RoleId);
        builder.HasIndex(e => new { e.RoleId, e.Code }).IsUnique().AreNullsDistinct(false);
    }
}
