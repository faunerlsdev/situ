using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Monetization;

namespace Situ.Infrastructure.Persistence.Configurations.Monetization;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.Property(e => e.Code).HasMaxLength(40);
        builder.Property(e => e.Name).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.HasIndex(e => e.Code).IsUnique().AreNullsDistinct(false);
    }
}
