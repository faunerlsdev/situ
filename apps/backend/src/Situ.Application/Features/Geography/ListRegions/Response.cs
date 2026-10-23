namespace Situ.Application.Features.Geography.ListRegions;

public sealed record ListRegionsResponse(IReadOnlyList<RegionItem> Regions);

public sealed record RegionItem(Guid Id, Guid CountryId, string Name, string? Code);
