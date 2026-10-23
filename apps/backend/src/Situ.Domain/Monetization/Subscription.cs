using Situ.Domain.Common;

namespace Situ.Domain.Monetization;

public enum SubscriptionStatus
{
    Active,
    PastDue,
    Cancelled,
    Expired,
}

/// <summary>A user's subscription to a paid plan. Users without one are on Free.</summary>
public sealed class Subscription : AggregateRoot
{
    private Subscription()
    {
    }

    public Guid UserId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid PriceId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? ExternalReference { get; private set; }

    public bool GrantsAccess(DateTimeOffset now) =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue && now < CurrentPeriodEnd;

    public static Subscription Start(Guid userId, Guid planId, Guid priceId, BillingPeriod period, string? externalReference, DateTimeOffset now)
    {
        var subscription = new Subscription
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            PlanId = Guard.NotEmpty(planId, nameof(planId)),
            PriceId = Guard.NotEmpty(priceId, nameof(priceId)),
            Status = SubscriptionStatus.Active,
            StartsAt = now,
            CurrentPeriodEnd = AddPeriod(now, period),
            ExternalReference = Guard.Optional(externalReference, nameof(externalReference), 200),
        };

        subscription.MarkCreated(now);
        return subscription;
    }

    public void Renew(BillingPeriod period, DateTimeOffset now)
    {
        Guard.That(Status != SubscriptionStatus.Cancelled && Status != SubscriptionStatus.Expired, "subscription.not_renewable", "The subscription cannot be renewed.");
        var from = CurrentPeriodEnd > now ? CurrentPeriodEnd : now;
        CurrentPeriodEnd = AddPeriod(from, period);
        Status = SubscriptionStatus.Active;
        MarkUpdated(now);
    }

    public void MarkPastDue(DateTimeOffset now)
    {
        if (Status == SubscriptionStatus.Active)
        {
            Status = SubscriptionStatus.PastDue;
            MarkUpdated(now);
        }
    }

    public void ChangePlan(Guid planId, Guid priceId, DateTimeOffset now)
    {
        PlanId = Guard.NotEmpty(planId, nameof(planId));
        PriceId = Guard.NotEmpty(priceId, nameof(priceId));
        MarkUpdated(now);
    }

    /// <summary>Keeps access until the paid period ends.</summary>
    public void Cancel(DateTimeOffset now)
    {
        CancelAtPeriodEnd = true;
        CancelledAt = now;
        MarkUpdated(now);
    }

    public void ExpireIfDue(DateTimeOffset now)
    {
        if (now < CurrentPeriodEnd)
        {
            return;
        }

        Status = CancelAtPeriodEnd ? SubscriptionStatus.Cancelled : SubscriptionStatus.Expired;
        MarkUpdated(now);
    }

    private static DateTimeOffset AddPeriod(DateTimeOffset from, BillingPeriod period) =>
        period == BillingPeriod.Monthly ? from.AddMonths(1) : from.AddYears(1);
}
