using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Live;

namespace Situ.Infrastructure.Persistence.Configurations.Live;

public sealed class LiveViewerSessionConfiguration : IEntityTypeConfiguration<LiveViewerSession>
{
    public void Configure(EntityTypeBuilder<LiveViewerSession> builder)
    {
        builder.HasIndex(e => e.LiveStreamId);
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasOne<LiveStream>().WithMany().HasForeignKey(e => e.LiveStreamId).OnDelete(DeleteBehavior.Cascade);
    }
}
