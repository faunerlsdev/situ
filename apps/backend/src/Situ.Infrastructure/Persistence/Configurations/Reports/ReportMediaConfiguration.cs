using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportMediaConfiguration : IEntityTypeConfiguration<ReportMedia>
{
    public void Configure(EntityTypeBuilder<ReportMedia> builder)
    {
        builder.Property(e => e.StorageKey).HasMaxLength(500);
        builder.Property(e => e.ThumbnailKey).HasMaxLength(500);
        builder.Optional<ReportMedia, Report>(e => e.ReportId);
        builder.Required<ReportMedia, User>(e => e.OwnerUserId);
        builder.HasIndex(e => e.ReportId);
    }
}
