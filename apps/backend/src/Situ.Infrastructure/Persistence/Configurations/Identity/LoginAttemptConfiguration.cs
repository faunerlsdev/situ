using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Identity;

public sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.Property(e => e.EmailAttempted).HasMaxLength(254);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.DeviceIdentifier).HasMaxLength(128);
        builder.Optional<LoginAttempt, User>(e => e.UserId);
        // LoginAttempt is not auditable; the timestamp column is occurred_at.
        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
    }
}
