using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Organizations;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Organizations;

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(80);
        builder.Required<AlertRule, Organization>(e => e.OrganizationId);
        builder.Optional<AlertRule, ReportCategory>(e => e.CategoryId);
        builder.Optional<AlertRule, City>(e => e.CityId);
    }
}
