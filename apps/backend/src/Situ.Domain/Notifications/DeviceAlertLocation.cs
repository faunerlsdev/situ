using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>
/// Approximate device location used only to decide proximity alerts.
/// Coordinates are rounded to 3 decimals (~110 m) and expire. They are never returned by the API.
/// </summary>
public sealed class DeviceAlertLocation : AuditableEntity
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    private DeviceAlertLocation()
    {
    }

    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public GeoPoint Location { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }

    public static DeviceAlertLocation Record(
        Guid userId,
        Guid deviceId,
        double latitude,
        double longitude,
        double? accuracyMeters,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        var location = new DeviceAlertLocation
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            DeviceId = Guard.NotEmpty(deviceId, nameof(deviceId)),
        };
        location.Apply(latitude, longitude, accuracyMeters, now, lifetime);
        location.MarkCreated(now);
        return location;
    }

    public bool CanRefresh(DateTimeOffset now) => now - ReportedAt >= MinimumInterval;

    public void Refresh(double latitude, double longitude, double? accuracyMeters, DateTimeOffset now, TimeSpan lifetime)
    {
        Guard.That(CanRefresh(now), "notification.alert_location_limited", "Wait a minute before updating the alert location.");
        Apply(latitude, longitude, accuracyMeters, now, lifetime);
        MarkUpdated(now);
    }

    public static double RoundCoordinate(double value) =>
        (double)Math.Round((decimal)value, 3, MidpointRounding.AwayFromZero);

    private void Apply(double latitude, double longitude, double? accuracyMeters, DateTimeOffset now, TimeSpan lifetime)
    {
        Guard.That(lifetime > TimeSpan.Zero, "notification.alert_location_lifetime", "Alert location lifetime must be positive.");
        Location = new GeoPoint(RoundCoordinate(latitude), RoundCoordinate(longitude));
        AccuracyMeters = accuracyMeters is { } accuracy
            ? Guard.InRange(accuracy, 0d, 10_000d, nameof(accuracyMeters))
            : null;
        ReportedAt = now;
        ExpiresAt = now.Add(lifetime);
    }
}
