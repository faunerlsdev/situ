using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class UserInterestCityConfiguration : IEntityTypeConfiguration<UserInterestCity>
{
    public void Configure(EntityTypeBuilder<UserInterestCity> builder)
    {
        builder.Required<UserInterestCity, User>(e => e.UserId);
        builder.Required<UserInterestCity, City>(e => e.CityId);
        builder.HasIndex(e => new { e.UserId, e.CityId }).IsUnique().AreNullsDistinct(false);
    }
}
