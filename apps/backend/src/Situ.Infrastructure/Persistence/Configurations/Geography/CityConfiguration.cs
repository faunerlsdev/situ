using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Geography;

public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.TimeZone).HasMaxLength(64);
        builder.Required<City, Region>(e => e.RegionId);
        builder.Required<City, Country>(e => e.CountryId);
        builder.HasIndex(e => new { e.RegionId, e.Name }).IsUnique().AreNullsDistinct(false);
    }
}
