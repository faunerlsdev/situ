using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Live;

public sealed class LiveStreamConfiguration : IEntityTypeConfiguration<LiveStream>
{
    public void Configure(EntityTypeBuilder<LiveStream> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(120);
        builder.Property(e => e.IngestKey).HasMaxLength(200);
        builder.Property(e => e.RecordingKey).HasMaxLength(500);
        builder.Required<LiveStream, User>(e => e.StreamerId);
        builder.Required<LiveStream, ReportCategory>(e => e.CategoryId);
        builder.Optional<LiveStream, City>(e => e.CityId);
        builder.Optional<LiveStream, Report>(e => e.ReportId);
    }
}
