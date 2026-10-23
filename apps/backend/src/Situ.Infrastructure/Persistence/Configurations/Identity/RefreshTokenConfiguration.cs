using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Identity;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Required<RefreshToken, User>(e => e.UserId);
        builder.Required<RefreshToken, UserDevice>(e => e.UserDeviceId);
        builder.Optional<RefreshToken, RefreshToken>(e => e.ReplacedByTokenId);
        builder.HasIndex(e => e.TokenHash).IsUnique().AreNullsDistinct(false);
    }
}
