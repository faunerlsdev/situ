using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.Ignore(e => e.HideAuthorOnMap);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Required<Report, User>(e => e.AuthorId);
        builder.Required<Report, ReportCategory>(e => e.CategoryId);
        builder.Optional<Report, City>(e => e.CityId);
        builder.Optional<Report, LiveStream>(e => e.SourceLiveStreamId);
        builder.Property(e => e.Visibility).HasMaxLength(40);
        builder.HasIndex(e => new { e.Status, e.ExpiresAt });
        builder.HasIndex(e => new { e.CityId, e.Status });
        builder.HasIndex(e => new { e.AuthorId, e.CreatedAt });
        builder.HasIndex(e => new { e.CityId, e.Status, e.ExpiresAt });
    }
}
