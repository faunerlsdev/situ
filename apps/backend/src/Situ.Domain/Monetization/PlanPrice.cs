using Situ.Domain.Common;

namespace Situ.Domain.Monetization;

public enum BillingPeriod
{
    Monthly,
    Yearly,
}

/// <summary>Price of a plan for a billing period in a currency (and optionally a country).</summary>
public sealed class PlanPrice : AuditableEntity
{
    private PlanPrice()
    {
        CurrencyCode = null!;
    }

    public Guid PlanId { get; private set; }
    public BillingPeriod Period { get; private set; }
    public string CurrencyCode { get; private set; }
    public Guid? CountryId { get; private set; }

    /// <summary>Amount in the currency's minor unit when it has one (cents), otherwise whole units.</summary>
    public long AmountMinor { get; private set; }

    public bool IsActive { get; private set; }

    public static PlanPrice Create(Guid planId, BillingPeriod period, string currencyCode, long amountMinor, Guid? countryId)
    {
        var code = Guard.NotEmpty(currencyCode, nameof(currencyCode), 3).ToUpperInvariant();
        Guard.That(code.Length == 3, "plan_price.invalid_currency", "Currency must be an ISO 4217 code.");
        Guard.That(amountMinor >= 0, "plan_price.negative", "Price cannot be negative.");

        return new PlanPrice
        {
            PlanId = Guard.NotEmpty(planId, nameof(planId)),
            Period = period,
            CurrencyCode = code,
            AmountMinor = amountMinor,
            CountryId = countryId,
            IsActive = true,
        };
    }

    public void ChangeAmount(long amountMinor)
    {
        Guard.That(amountMinor >= 0, "plan_price.negative", "Price cannot be negative.");
        AmountMinor = amountMinor;
    }

    public void Deactivate() => IsActive = false;
}
