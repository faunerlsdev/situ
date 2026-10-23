using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Monetization;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(e => e.CurrencyCode).HasMaxLength(3);
        builder.Property(e => e.Provider).HasMaxLength(40);
        builder.Property(e => e.ProviderReference).HasMaxLength(200);
        builder.Property(e => e.FailureReason).HasMaxLength(300);
        builder.Required<Payment, User>(e => e.UserId);
        builder.Optional<Payment, Subscription>(e => e.SubscriptionId);
        builder.Optional<Payment, SponsoredPin>(e => e.SponsoredPinId);
    }
}
