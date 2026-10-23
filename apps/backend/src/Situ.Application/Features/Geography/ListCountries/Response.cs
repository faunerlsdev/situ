using Situ.Domain.Geography;

namespace Situ.Application.Features.Geography.ListCountries;

public sealed record ListCountriesResponse(IReadOnlyList<CountryItem> Countries);

public sealed record CountryItem(
    Guid Id,
    string IsoCode,
    string Name,
    string RegionLabel,
    string DefaultLocale,
    string DefaultCurrency,
    string DefaultTimeZone,
    DistanceUnit DistanceUnit,
    int MinimumAge,
    bool IsEnabled);
