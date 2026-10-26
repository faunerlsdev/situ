using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Reports;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class MapZoneNoticeConfiguration : IEntityTypeConfiguration<MapZoneNotice>
{
    public void Configure(EntityTypeBuilder<MapZoneNotice> builder)
    {
        builder.Property(notice => notice.Type).HasMaxLength(32);
        builder.Property(notice => notice.CellId).HasMaxLength(32);
        builder.HasIndex(notice => new { notice.Status, notice.OccurredAt });
    }
}
