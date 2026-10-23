using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Administration;

namespace Situ.Infrastructure.Persistence.Configurations.Administration;

public sealed class StaffRoleConfiguration : IEntityTypeConfiguration<StaffRole>
{
    public void Configure(EntityTypeBuilder<StaffRole> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.HasIndex(e => e.Name).IsUnique().AreNullsDistinct(false);
    }
}
