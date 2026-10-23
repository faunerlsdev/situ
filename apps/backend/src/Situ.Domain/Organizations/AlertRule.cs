using Situ.Domain.Common;

namespace Situ.Domain.Organizations;

/// <summary>
/// Rule an organization sets up to be alerted about reports (e.g. a transit authority wants every
/// "Accidente de tránsito" within 3 km of a point).
/// </summary>
public sealed class AlertRule : AuditableEntity
{
    private AlertRule()
    {
        Name = null!;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? CityId { get; private set; }
    public GeoPoint? Center { get; private set; }
    public double? RadiusMeters { get; private set; }
    public bool IsActive { get; private set; }

    public static AlertRule Create(
        Guid organizationId,
        string name,
        Guid? categoryId,
        Guid? cityId,
        GeoPoint? center,
        double? radiusMeters,
        DateTimeOffset now)
    {
        var rule = new AlertRule
        {
            OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId)),
            IsActive = true,
        };
        rule.Apply(name, categoryId, cityId, center, radiusMeters);
        rule.MarkCreated(now);
        return rule;
    }

    public void Update(string name, Guid? categoryId, Guid? cityId, GeoPoint? center, double? radiusMeters, DateTimeOffset now)
    {
        Apply(name, categoryId, cityId, center, radiusMeters);
        MarkUpdated(now);
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public bool Matches(Guid categoryId, Guid? cityId, GeoPoint location)
    {
        if (!IsActive)
        {
            return false;
        }

        if (CategoryId is { } c && c != categoryId)
        {
            return false;
        }

        if (CityId is { } rc && rc != cityId)
        {
            return false;
        }

        return Center is not { } center || RadiusMeters is not { } radius || location.IsWithin(center, radius);
    }

    private void Apply(string name, Guid? categoryId, Guid? cityId, GeoPoint? center, double? radiusMeters)
    {
        Guard.That((center is null) == (radiusMeters is null), "alert_rule.area", "Center and radius go together.");

        Name = Guard.NotEmpty(name, nameof(name), 80);
        CategoryId = categoryId;
        CityId = cityId;
        Center = center;
        RadiusMeters = radiusMeters is { } r ? Guard.InRange(r, 100d, 50_000d, nameof(radiusMeters)) : null;
    }
}
