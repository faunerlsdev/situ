using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Monetization;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Monetization;

public sealed class PlanPriceConfiguration : IEntityTypeConfiguration<PlanPrice>
{
    public void Configure(EntityTypeBuilder<PlanPrice> builder)
    {
        builder.Property(e => e.CurrencyCode).HasMaxLength(3);
        builder.Required<PlanPrice, SubscriptionPlan>(e => e.PlanId);
        builder.Optional<PlanPrice, Country>(e => e.CountryId);
        builder.HasIndex(e => new { e.PlanId, e.Period, e.CurrencyCode, e.CountryId }).IsUnique().AreNullsDistinct(false);
    }
}
