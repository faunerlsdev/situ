using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.News;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.News;

public sealed class UserReputationConfiguration : IEntityTypeConfiguration<UserReputation>
{
    public void Configure(EntityTypeBuilder<UserReputation> builder)
    {
        builder.Required<UserReputation, User>(e => e.UserId);
        builder.HasIndex(e => e.UserId).IsUnique().AreNullsDistinct(false);
    }
}
