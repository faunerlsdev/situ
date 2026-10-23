using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportCategoryConfiguration : IEntityTypeConfiguration<ReportCategory>
{
    public void Configure(EntityTypeBuilder<ReportCategory> builder)
    {
        builder.Property(e => e.Code).HasMaxLength(40);
        builder.Property(e => e.Name).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(160);
        builder.Property(e => e.ButtonIconUrl).HasMaxLength(500);
        builder.Property(e => e.MapIconUrl).HasMaxLength(500);
        builder.Property(e => e.ColorHex).HasMaxLength(7);
        builder.Optional<ReportCategory, ReportCategory>(e => e.ParentCategoryId);
        builder.HasIndex(e => e.Code).IsUnique().AreNullsDistinct(false);
    }
}
