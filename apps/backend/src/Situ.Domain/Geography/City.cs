using Situ.Domain.Common;

namespace Situ.Domain.Geography;

/// <summary>City. The one chosen at registration is the user's "city of interest" for notifications (RF-NOT-02).</summary>
public sealed class City : AuditableEntity
{
    private City()
    {
        Name = null!;
    }

    public Guid RegionId { get; private set; }

    /// <summary>Denormalized for fast filtering by country.</summary>
    public Guid CountryId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Approximate center, used to center the map and to locate reports by city.</summary>
    public GeoPoint Center { get; private set; }

    public string? TimeZone { get; private set; }
    public bool IsEnabled { get; private set; }

    public static City Create(Guid regionId, Guid countryId, string name, GeoPoint center, string? timeZone = null) => new()
    {
        RegionId = Guard.NotEmpty(regionId, nameof(regionId)),
        CountryId = Guard.NotEmpty(countryId, nameof(countryId)),
        Name = Guard.NotEmpty(name, nameof(name), 100),
        Center = center,
        TimeZone = Guard.Optional(timeZone, nameof(timeZone), 64),
        IsEnabled = true,
    };

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;
}
