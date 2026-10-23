using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Profiles;

public sealed class UserLocationConfiguration : IEntityTypeConfiguration<UserLocation>
{
    public void Configure(EntityTypeBuilder<UserLocation> builder)
    {
        builder.Required<UserLocation, User>(e => e.UserId);
        builder.Optional<UserLocation, City>(e => e.CityId);
        builder.HasIndex(e => e.UserId).IsUnique().AreNullsDistinct(false);
    }
}
