using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Geography.ListRegions;

public sealed class ListRegionsHandler(IApplicationDbContext db)
    : IHandler<ListRegionsRequest, Result<ListRegionsResponse>>
{
    public async Task<Result<ListRegionsResponse>> Handle(
        ListRegionsRequest request,
        CancellationToken cancellationToken)
    {
        var countryEnabled = await db.Countries
            .AnyAsync(country => country.Id == request.CountryId && country.IsEnabled, cancellationToken);
        if (!countryEnabled)
        {
            return Result<ListRegionsResponse>.Failure(
                Error.NotFound("geography.country_not_found", "Ese país no está disponible."));
        }

        var regions = await db.Regions
            .AsNoTracking()
            .Where(region => region.CountryId == request.CountryId && region.IsEnabled)
            .OrderBy(region => region.Name)
            .Select(region => new RegionItem(region.Id, region.CountryId, region.Name, region.Code))
            .ToListAsync(cancellationToken);

        return Result<ListRegionsResponse>.Success(new ListRegionsResponse(regions));
    }
}
