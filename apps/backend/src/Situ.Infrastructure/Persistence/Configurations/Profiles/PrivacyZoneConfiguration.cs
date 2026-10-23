using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Profiles;

public sealed class PrivacyZoneConfiguration : IEntityTypeConfiguration<PrivacyZone>
{
    public void Configure(EntityTypeBuilder<PrivacyZone> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(60);
        builder.Required<PrivacyZone, User>(e => e.UserId);
    }
}
