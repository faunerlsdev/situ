using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(e => e.Locale).HasMaxLength(15);
        builder.Required<User, Country>(e => e.CountryId);
        builder.Required<User, Region>(e => e.RegionId);
        builder.Required<User, City>(e => e.CityId);
        builder.HasIndex(e => e.Email).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(e => e.UserName).IsUnique().AreNullsDistinct(false);
    }
}
