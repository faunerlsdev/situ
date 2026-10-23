using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Live;

public sealed class LiveViewerConfiguration : IEntityTypeConfiguration<LiveViewer>
{
    public void Configure(EntityTypeBuilder<LiveViewer> builder)
    {
        builder.Required<LiveViewer, LiveStream>(e => e.LiveStreamId);
        builder.Required<LiveViewer, User>(e => e.UserId);
    }
}
