using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Geography.ListCities;

public sealed class ListCitiesHandler(IApplicationDbContext db)
    : IHandler<ListCitiesRequest, Result<ListCitiesResponse>>
{
    public async Task<Result<ListCitiesResponse>> Handle(
        ListCitiesRequest request,
        CancellationToken cancellationToken)
    {
        var region = await db.Regions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.RegionId && item.IsEnabled, cancellationToken);
        if (region is null)
        {
            return Result<ListCitiesResponse>.Failure(
                Error.NotFound("geography.region_not_found", "Ese departamento no está disponible."));
        }

        var cities = await db.Cities
            .AsNoTracking()
            .Where(city => city.RegionId == region.Id && city.IsEnabled)
            .OrderBy(city => city.Name)
            .Select(city => new CityItem(city.Id, city.RegionId, city.CountryId, city.Name))
            .ToListAsync(cancellationToken);

        return Result<ListCitiesResponse>.Success(new ListCitiesResponse(cities));
    }
}
