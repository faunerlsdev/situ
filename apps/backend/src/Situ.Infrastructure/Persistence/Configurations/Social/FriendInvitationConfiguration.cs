using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Social;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Social;

public sealed class FriendInvitationConfiguration : IEntityTypeConfiguration<FriendInvitation>
{
    public void Configure(EntityTypeBuilder<FriendInvitation> builder)
    {
        builder.Property(e => e.Email).HasMaxLength(254);
        builder.Required<FriendInvitation, User>(e => e.InviterId);
        builder.Optional<FriendInvitation, User>(e => e.AcceptedByUserId);
        builder.HasIndex(e => e.TokenHash).IsUnique().AreNullsDistinct(false);
    }
}
