using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>
/// Last known position of a user, written by the mobile app (RF-UBI-07). Hot reads (nearby users, 1 km alerts)
/// are served from Redis GEO; this row is the durable fallback.
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

    /// <summary>"Last seen X ago" instead of a moving dot when the signal is stale (RF-UBI-08).</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => now - UpdatedAt > maxAge;
}
