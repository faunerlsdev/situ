using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Anonymity;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Anonymity;

public sealed class AnonymousParticipantConfiguration : IEntityTypeConfiguration<AnonymousParticipant>
{
    public void Configure(EntityTypeBuilder<AnonymousParticipant> builder)
    {
        builder.Required<AnonymousParticipant, User>(e => e.UserId);
        builder.HasIndex(e => new { e.TargetType, e.TargetId, e.UserId }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(e => new { e.TargetType, e.TargetId, e.Index }).IsUnique().AreNullsDistinct(false);
    }
}
