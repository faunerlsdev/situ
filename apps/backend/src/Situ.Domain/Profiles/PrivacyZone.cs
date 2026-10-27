using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>Area (home, work) where the user's real-time location is never shown (RF-UBI-10).</summary>
public enum PrivacyZoneMode
{
    Hidden,
    Approximate,
}

public sealed class PrivacyZone : AuditableEntity
{
    private PrivacyZone()
    {
        Name = null!;
    }

    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public GeoPoint Center { get; private set; }
    public int RadiusMeters { get; private set; }
    public PrivacyZoneMode Mode { get; private set; }

    public static PrivacyZone Create(Guid userId, string name, GeoPoint center, int radiusMeters, PrivacyZoneMode mode = PrivacyZoneMode.Hidden) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        Name = Guard.NotEmpty(name, nameof(name), 60),
        Center = center,
        RadiusMeters = Guard.InRange(radiusMeters, 50, 2000, nameof(radiusMeters)),
        Mode = mode,
    };

    public void Update(string name, GeoPoint center, int radiusMeters, PrivacyZoneMode mode)
    {
        Name = Guard.NotEmpty(name, nameof(name), 60);
        Center = center;
        RadiusMeters = Guard.InRange(radiusMeters, 50, 2000, nameof(radiusMeters));
        Mode = mode;
    }

    public bool Contains(GeoPoint point) => Center.IsWithin(point, RadiusMeters);
}
