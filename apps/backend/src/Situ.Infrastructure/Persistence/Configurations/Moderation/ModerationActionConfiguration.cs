using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Moderation;

public sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> builder)
    {
        builder.Property(e => e.Reason).HasMaxLength(1000);
        builder.Required<ModerationAction, User>(e => e.StaffId);
        builder.Optional<ModerationAction, Complaint>(e => e.ComplaintId);
    }
}
