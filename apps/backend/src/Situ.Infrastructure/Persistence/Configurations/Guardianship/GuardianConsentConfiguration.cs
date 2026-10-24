using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Guardianship;

public sealed class GuardianConsentConfiguration : IEntityTypeConfiguration<GuardianConsent>
{
    public void Configure(EntityTypeBuilder<GuardianConsent> builder)
    {
        builder.Property(e => e.GuardianEmail).HasMaxLength(254);
        builder.Property(e => e.NoticeVersion).HasMaxLength(30);
        builder.Property(e => e.GuardianFullName).HasMaxLength(150);
        builder.Property(e => e.GuardianDocumentNumber).HasMaxLength(30);
        builder.Property(e => e.GuardianContactEmail).HasMaxLength(254);
        builder.Property(e => e.DigitalSignature).HasMaxLength(512);
        builder.Property(e => e.AuthorizationIpAddress).HasMaxLength(64);
        builder.Property(e => e.AuthorizationUserAgent).HasMaxLength(512);
        builder.Property(e => e.AuthorizationChannel).HasMaxLength(30);
        builder.Property(e => e.DeclineReason).HasMaxLength(500);
        builder.Property(e => e.MinorStatement).HasMaxLength(500);
        builder.Required<GuardianConsent, User>(e => e.MinorUserId);
        builder.HasIndex(e => e.TokenHash).IsUnique().AreNullsDistinct(false);
    }
}
