using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportCategoryTranslationConfiguration : IEntityTypeConfiguration<ReportCategoryTranslation>
{
    public void Configure(EntityTypeBuilder<ReportCategoryTranslation> builder)
    {
        builder.Property(e => e.Locale).HasMaxLength(15);
        builder.Property(e => e.Name).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(160);
        builder.Required<ReportCategoryTranslation, ReportCategory>(e => e.CategoryId);
        builder.HasIndex(e => new { e.CategoryId, e.Locale }).IsUnique().AreNullsDistinct(false);
    }
}
