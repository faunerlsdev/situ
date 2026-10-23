using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Geography;

public sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.Code).HasMaxLength(10);
        builder.Required<Region, Country>(e => e.CountryId);
        builder.HasIndex(e => new { e.CountryId, e.Name }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(e => new { e.CountryId, e.Code }).IsUnique().AreNullsDistinct(false);
    }
}
