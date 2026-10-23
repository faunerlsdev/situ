namespace Situ.Application.Features.Geography.ListCities;

public sealed record ListCitiesResponse(IReadOnlyList<CityItem> Cities);

public sealed record CityItem(Guid Id, Guid RegionId, Guid CountryId, string Name);
