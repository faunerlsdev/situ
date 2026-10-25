using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Social;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Social;

public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.Required<UserBlock, User>(e => e.BlockerId);
        builder.Required<UserBlock, User>(e => e.BlockedUserId);
        builder.HasIndex(e => new { e.BlockerId, e.BlockedUserId, e.Scope }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(e => new { e.BlockedUserId, e.Scope });
        builder.HasIndex(e => new { e.BlockerId, e.Scope });
    }
}
