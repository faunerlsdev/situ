using Situ.Domain.Common;

namespace Situ.Domain.Monetization;

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded,
}

/// <summary>Payment record. Card data never touches Situ: only the provider reference is stored.</summary>
public sealed class Payment : AuditableEntity
{
    private Payment()
    {
        CurrencyCode = null!;
        Provider = null!;
    }

    public Guid UserId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public Guid? SponsoredPinId { get; private set; }
    public long AmountMinor { get; private set; }
    public string CurrencyCode { get; private set; }
    public string Provider { get; private set; }
    public string? ProviderReference { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? FailureReason { get; private set; }

    public static Payment Create(
        Guid userId,
        Guid? subscriptionId,
        Guid? sponsoredPinId,
        long amountMinor,
        string currencyCode,
        string provider,
        DateTimeOffset now)
    {
        Guard.That(amountMinor > 0, "payment.invalid_amount", "Amount must be positive.");
        Guard.That(subscriptionId is not null ^ sponsoredPinId is not null, "payment.single_target", "A payment is for a subscription or a sponsored pin.");

        var payment = new Payment
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            SubscriptionId = subscriptionId,
            SponsoredPinId = sponsoredPinId,
            AmountMinor = amountMinor,
            CurrencyCode = Guard.NotEmpty(currencyCode, nameof(currencyCode), 3).ToUpperInvariant(),
            Provider = Guard.NotEmpty(provider, nameof(provider), 40),
            Status = PaymentStatus.Pending,
        };

        payment.MarkCreated(now);
        return payment;
    }

    public void MarkSucceeded(string providerReference, DateTimeOffset now)
    {
        Guard.That(Status == PaymentStatus.Pending, "payment.not_pending", "The payment is not pending.");
        ProviderReference = Guard.NotEmpty(providerReference, nameof(providerReference), 200);
        Status = PaymentStatus.Succeeded;
        PaidAt = now;
        MarkUpdated(now);
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        Guard.That(Status == PaymentStatus.Pending, "payment.not_pending", "The payment is not pending.");
        Status = PaymentStatus.Failed;
        FailureReason = Guard.Optional(reason, nameof(reason), 300);
        MarkUpdated(now);
    }

    public void Refund(DateTimeOffset now)
    {
        Guard.That(Status == PaymentStatus.Succeeded, "payment.not_refundable", "Only successful payments can be refunded.");
        Status = PaymentStatus.Refunded;
        MarkUpdated(now);
    }
}
