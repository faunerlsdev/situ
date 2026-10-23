using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Monetization;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.Property(e => e.ExternalReference).HasMaxLength(200);
        builder.Required<Subscription, User>(e => e.UserId);
        builder.Required<Subscription, SubscriptionPlan>(e => e.PlanId);
        builder.Required<Subscription, PlanPrice>(e => e.PriceId);
    }
}
