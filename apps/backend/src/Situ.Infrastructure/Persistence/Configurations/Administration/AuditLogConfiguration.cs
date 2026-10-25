using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Administration;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Administration;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(e => e.Action).HasMaxLength(100);
        builder.Property(e => e.EntityType).HasMaxLength(100);
        builder.Property(e => e.EntityId).HasMaxLength(100);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Optional<AuditLog, User>(e => e.ActorUserId);
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.ActorUserId, e.OccurredAt });
        builder.HasIndex(e => new { e.Action, e.EntityType });
    }
}
