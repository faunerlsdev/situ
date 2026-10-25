using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Moderation;

public sealed class TakedownRequestConfiguration : IEntityTypeConfiguration<TakedownRequest>
{
    public void Configure(EntityTypeBuilder<TakedownRequest> builder)
    {
        builder.Property(e => e.RequesterName).HasMaxLength(120);
        builder.Property(e => e.RequesterEmail).HasMaxLength(254);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Resolution).HasMaxLength(2000);
        builder.Optional<TakedownRequest, User>(e => e.HandledByStaffId);
        builder.HasIndex(e => e.RequesterEmail);
        builder.HasIndex(e => new { e.Status, e.DueAt });
    }
}
