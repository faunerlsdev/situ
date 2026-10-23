using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportCategoryOverrideConfiguration : IEntityTypeConfiguration<ReportCategoryOverride>
{
    public void Configure(EntityTypeBuilder<ReportCategoryOverride> builder)
    {
        builder.Required<ReportCategoryOverride, ReportCategory>(e => e.CategoryId);
        builder.Optional<ReportCategoryOverride, Country>(e => e.CountryId);
        builder.Optional<ReportCategoryOverride, City>(e => e.CityId);
    }
}
