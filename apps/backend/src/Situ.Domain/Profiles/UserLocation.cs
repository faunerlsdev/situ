using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>
/// Last rounded position shared with friends or the public map.
/// The raw GPS reading is never stored. Proximity alerts use <c>DeviceAlertLocation</c>.
/// </summary>
public sealed class UserLocation : Entity
{
    private UserLocation()
    {
    }

    public Guid UserId { get; private set; }
    public GeoPoint Point { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public Guid? CityId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public bool IsLive { get; private set; }

    public static UserLocation Create(Guid userId, GeoPoint point, double? accuracyMeters, Guid? cityId, DateTimeOffset now) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        Point = point,
        AccuracyMeters = accuracyMeters,
        CityId = cityId,
        UpdatedAt = now,
    };

    public void Update(GeoPoint point, double? accuracyMeters, Guid? cityId, DateTimeOffset now)
    {
        Point = point;
        AccuracyMeters = accuracyMeters;
        CityId = cityId;
        UpdatedAt = now;
    }

    public void Publish(GeoPoint point, double? accuracyMeters, Guid? cityId, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Point = point;
        AccuracyMeters = accuracyMeters;
        CityId = cityId;
        UpdatedAt = now;
        ExpiresAt = expiresAt;
        IsLive = true;
        LastSeenAt = null;
    }

    public void MarkLastSeen(DateTimeOffset seenAt, DateTimeOffset expiresAt)
    {
        IsLive = false;
        LastSeenAt = seenAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>"Last seen X ago" instead of a moving dot when the signal is stale (RF-UBI-08).</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => now - UpdatedAt > maxAge;
}
