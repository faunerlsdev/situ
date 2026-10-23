using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;
using Situ.Domain.Organizations;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Monetization;

public sealed class SponsoredPinConfiguration : IEntityTypeConfiguration<SponsoredPin>
{
    public void Configure(EntityTypeBuilder<SponsoredPin> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(80);
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.Property(e => e.ImageKey).HasMaxLength(500);
        builder.Property(e => e.RejectionReason).HasMaxLength(300);
        builder.Required<SponsoredPin, User>(e => e.OwnerUserId);
        builder.Optional<SponsoredPin, Organization>(e => e.OrganizationId);
        builder.Optional<SponsoredPin, City>(e => e.CityId);
    }
}
