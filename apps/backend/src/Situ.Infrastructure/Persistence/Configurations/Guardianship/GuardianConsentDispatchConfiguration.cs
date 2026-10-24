using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Guardianship;

public sealed class GuardianConsentDispatchConfiguration : IEntityTypeConfiguration<GuardianConsentDispatch>
{
    public void Configure(EntityTypeBuilder<GuardianConsentDispatch> builder)
    {
        builder.Property(e => e.DestinationEmail).HasMaxLength(254);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Required<GuardianConsentDispatch, User>(e => e.MinorUserId);
        builder.HasIndex(e => e.DestinationEmail);
        builder.HasIndex(e => e.SentAt);
    }
}
