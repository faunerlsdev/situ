using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportMediaConfiguration : IEntityTypeConfiguration<ReportMedia>
{
    public void Configure(EntityTypeBuilder<ReportMedia> builder)
    {
        builder.Property(e => e.StorageKey).HasMaxLength(500);
        builder.Property(e => e.ThumbnailKey).HasMaxLength(500);
        builder.Required<ReportMedia, Report>(e => e.ReportId);
    }
}
