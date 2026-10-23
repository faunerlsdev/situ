using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Profiles;

public sealed class LocationShareRecipientConfiguration : IEntityTypeConfiguration<LocationShareRecipient>
{
    public void Configure(EntityTypeBuilder<LocationShareRecipient> builder)
    {
        builder.Required<LocationShareRecipient, User>(e => e.UserId);
        builder.Required<LocationShareRecipient, User>(e => e.FriendUserId);
        builder.HasIndex(e => new { e.UserId, e.FriendUserId }).IsUnique().AreNullsDistinct(false);
    }
}
