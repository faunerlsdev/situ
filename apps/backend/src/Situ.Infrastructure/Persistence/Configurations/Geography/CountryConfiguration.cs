using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;

namespace Situ.Infrastructure.Persistence.Configurations.Geography;

public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.Property(e => e.IsoCode).HasMaxLength(2);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.RegionLabel).HasMaxLength(50);
        builder.Property(e => e.DefaultLocale).HasMaxLength(15);
        builder.Property(e => e.DefaultCurrency).HasMaxLength(3);
        builder.Property(e => e.DefaultTimeZone).HasMaxLength(64);
        builder.HasIndex(e => e.IsoCode).IsUnique().AreNullsDistinct(false);
    }
}
