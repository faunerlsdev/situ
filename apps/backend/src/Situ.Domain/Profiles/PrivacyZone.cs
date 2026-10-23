using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>Area (home, work) where the user's real-time location is never shown (RF-UBI-10).</summary>
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

    public static PrivacyZone Create(Guid userId, string name, GeoPoint center, int radiusMeters) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        Name = Guard.NotEmpty(name, nameof(name), 60),
        Center = center,
        RadiusMeters = Guard.InRange(radiusMeters, 50, 2000, nameof(radiusMeters)),
    };

    public bool Contains(GeoPoint point) => Center.IsWithin(point, RadiusMeters);
}
