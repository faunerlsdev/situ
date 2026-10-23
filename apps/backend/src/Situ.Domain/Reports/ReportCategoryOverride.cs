using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>Enables/disables a category or changes its TTL for a country or a city (RF-GLO-06, RF-CAT-03).</summary>
public sealed class ReportCategoryOverride : AuditableEntity
{
    private ReportCategoryOverride()
    {
    }

    public Guid CategoryId { get; private set; }
    public Guid? CountryId { get; private set; }
    public Guid? CityId { get; private set; }
    public bool IsEnabled { get; private set; }
    public int? TtlMinutes { get; private set; }

    public static ReportCategoryOverride Create(Guid categoryId, Guid? countryId, Guid? cityId, bool isEnabled, int? ttlMinutes)
    {
        Guard.That(countryId is not null || cityId is not null, "report_category_override.scope_required", "A country or a city is required.");

        return new ReportCategoryOverride
        {
            CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId)),
            CountryId = countryId,
            CityId = cityId,
            IsEnabled = isEnabled,
            TtlMinutes = ttlMinutes is { } ttl ? Guard.InRange(ttl, 5, 60 * 24 * 30, nameof(ttlMinutes)) : null,
        };
    }

    public void Update(bool isEnabled, int? ttlMinutes)
    {
        IsEnabled = isEnabled;
        TtlMinutes = ttlMinutes is { } ttl ? Guard.InRange(ttl, 5, 60 * 24 * 30, nameof(ttlMinutes)) : null;
    }
}
