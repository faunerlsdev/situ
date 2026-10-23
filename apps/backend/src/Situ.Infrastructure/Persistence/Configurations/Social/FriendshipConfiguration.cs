using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Social;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Social;

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.Required<Friendship, User>(e => e.RequesterId);
        builder.Required<Friendship, User>(e => e.AddresseeId);
        builder.HasIndex(e => new { e.RequesterId, e.AddresseeId }).IsUnique().AreNullsDistinct(false);
    }
}
