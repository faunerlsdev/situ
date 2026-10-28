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
        builder.Property(e => e.RecordingPath).HasMaxLength(500);
        builder.Property(e => e.PublicPath).HasMaxLength(64);
        builder.Property(e => e.PublishKeyHash).HasMaxLength(128);
        builder.Property(e => e.HiddenReason).HasMaxLength(300);
        builder.HasIndex(e => e.PublicPath).IsUnique();
        builder.HasIndex(e => new { e.Status, e.Visibility });
        builder.Required<LiveStream, User>(e => e.StreamerId);
        builder.Optional<LiveStream, ReportCategory>(e => e.CategoryId);
        builder.Optional<LiveStream, City>(e => e.CityId);
        builder.Optional<LiveStream, Report>(e => e.ReportId);
    }
}
