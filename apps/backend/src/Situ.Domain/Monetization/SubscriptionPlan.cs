using Situ.Domain.Common;

namespace Situ.Domain.Monetization;

public enum PlanTier
{
    Free,
    Basic,
    Pro,
    Advanced,
}

/// <summary>
/// Plan definition (Free, Basic, Pro, Advanced). Limits live in the database so they can change without a release (RF-SUB-01..05).
/// A null limit means unlimited.
/// </summary>
public sealed class SubscriptionPlan : AggregateRoot
{
    private SubscriptionPlan()
    {
        Code = null!;
        Name = null!;
        Description = null!;
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public PlanTier Tier { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    public bool ShowsAds { get; private set; }
    public int? MaxDevices { get; private set; }
    public int? MaxLiveMinutesPerStream { get; private set; }
    public int? MaxReportsPerDay { get; private set; }
    public int? MaxAnonymousPostsPerDay { get; private set; }
    public int? MaxSavedRecordings { get; private set; }
    public bool CanSponsorPins { get; private set; }

    public static SubscriptionPlan Create(
        string code,
        string name,
        string description,
        PlanTier tier,
        int sortOrder,
        bool showsAds,
        int? maxDevices,
        int? maxLiveMinutesPerStream,
        int? maxReportsPerDay,
        int? maxAnonymousPostsPerDay,
        int? maxSavedRecordings,
        bool canSponsorPins)
    {
        var plan = new SubscriptionPlan
        {
            Code = Guard.NotEmpty(code, nameof(code), 40).ToLowerInvariant(),
            Tier = tier,
            IsActive = true,
        };
        plan.Apply(name, description, sortOrder, showsAds, maxDevices, maxLiveMinutesPerStream, maxReportsPerDay, maxAnonymousPostsPerDay, maxSavedRecordings, canSponsorPins);
        return plan;
    }

    public void Update(
        string name,
        string description,
        int sortOrder,
        bool showsAds,
        int? maxDevices,
        int? maxLiveMinutesPerStream,
        int? maxReportsPerDay,
        int? maxAnonymousPostsPerDay,
        int? maxSavedRecordings,
        bool canSponsorPins) =>
        Apply(name, description, sortOrder, showsAds, maxDevices, maxLiveMinutesPerStream, maxReportsPerDay, maxAnonymousPostsPerDay, maxSavedRecordings, canSponsorPins);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private void Apply(
        string name,
        string description,
        int sortOrder,
        bool showsAds,
        int? maxDevices,
        int? maxLiveMinutesPerStream,
        int? maxReportsPerDay,
        int? maxAnonymousPostsPerDay,
        int? maxSavedRecordings,
        bool canSponsorPins)
    {
        Name = Guard.NotEmpty(name, nameof(name), 60);
        Description = Guard.NotEmpty(description, nameof(description), 300);
        SortOrder = sortOrder;
        ShowsAds = showsAds;
        MaxDevices = Limit(maxDevices, nameof(maxDevices));
        MaxLiveMinutesPerStream = Limit(maxLiveMinutesPerStream, nameof(maxLiveMinutesPerStream));
        MaxReportsPerDay = Limit(maxReportsPerDay, nameof(maxReportsPerDay));
        MaxAnonymousPostsPerDay = Limit(maxAnonymousPostsPerDay, nameof(maxAnonymousPostsPerDay));
        MaxSavedRecordings = Limit(maxSavedRecordings, nameof(maxSavedRecordings));
        CanSponsorPins = canSponsorPins;
    }

    private static int? Limit(int? value, string name) =>
        value is { } v ? Guard.InRange(v, 0, int.MaxValue, name) : null;
}
