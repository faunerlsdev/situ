using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.News;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.News;

public sealed class ImportanceScoringConfigConfiguration : IEntityTypeConfiguration<ImportanceScoringConfig>
{
    public void Configure(EntityTypeBuilder<ImportanceScoringConfig> builder)
    {
        builder.Optional<ImportanceScoringConfig, Country>(e => e.CountryId);
    }
}
