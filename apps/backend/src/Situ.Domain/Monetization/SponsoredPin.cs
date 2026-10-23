using Situ.Domain.Common;

namespace Situ.Domain.Monetization;

public enum SponsoredPinStatus
{
    PendingReview,
    Approved,
    Rejected,
    Expired,
}

/// <summary>Business pin shown on the map (bar, store, event). Clearly labelled as sponsored and never shown to minors (RF-PUB-01..04).</summary>
public sealed class SponsoredPin : AggregateRoot
{
    private SponsoredPin()
    {
        Title = null!;
        Description = null!;
    }

    public Guid OwnerUserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? CityId { get; private set; }
    public GeoPoint Location { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string? ImageKey { get; private set; }
    public string? LinkUrl { get; private set; }
    public SponsoredPinStatus Status { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public bool IsShownAt(DateTimeOffset now) =>
        Status == SponsoredPinStatus.Approved && now >= StartsAt && now < EndsAt;

    public static SponsoredPin Create(
        Guid ownerUserId,
        Guid? organizationId,
        Guid? cityId,
        GeoPoint location,
        string title,
        string description,
        string? imageKey,
        string? linkUrl,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now)
    {
        Guard.That(endsAt > startsAt, "sponsored_pin.invalid_dates", "End date must be after the start date.");

        if (linkUrl is not null)
        {
            Guard.That(
                Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "sponsored_pin.invalid_link",
                "The link must be an https URL.");
        }

        var pin = new SponsoredPin
        {
            OwnerUserId = Guard.NotEmpty(ownerUserId, nameof(ownerUserId)),
            OrganizationId = organizationId,
            CityId = cityId,
            Location = location,
            Title = Guard.NotEmpty(title, nameof(title), 80),
            Description = Guard.NotEmpty(description, nameof(description), 300),
            ImageKey = Guard.Optional(imageKey, nameof(imageKey), 500),
            LinkUrl = linkUrl,
            Status = SponsoredPinStatus.PendingReview,
            StartsAt = startsAt,
            EndsAt = endsAt,
        };

        pin.MarkCreated(now);
        return pin;
    }

    public void Approve(DateTimeOffset now)
    {
        Guard.That(Status == SponsoredPinStatus.PendingReview, "sponsored_pin.not_pending", "Only pins under review can be approved.");
        Status = SponsoredPinStatus.Approved;
        RejectionReason = null;
        MarkUpdated(now);
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        Guard.That(Status == SponsoredPinStatus.PendingReview, "sponsored_pin.not_pending", "Only pins under review can be rejected.");
        Status = SponsoredPinStatus.Rejected;
        RejectionReason = Guard.NotEmpty(reason, nameof(reason), 300);
        MarkUpdated(now);
    }

    public void ExpireIfDue(DateTimeOffset now)
    {
        if (Status == SponsoredPinStatus.Approved && now >= EndsAt)
        {
            Status = SponsoredPinStatus.Expired;
            MarkUpdated(now);
        }
    }
}
