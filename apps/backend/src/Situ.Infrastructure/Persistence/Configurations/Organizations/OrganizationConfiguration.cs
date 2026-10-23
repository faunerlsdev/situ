using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Organizations;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Organizations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.Slug).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.LogoKey).HasMaxLength(500);
        builder.Optional<Organization, City>(e => e.CityId);
        builder.HasIndex(e => e.Slug).IsUnique().AreNullsDistinct(false);
    }
}
