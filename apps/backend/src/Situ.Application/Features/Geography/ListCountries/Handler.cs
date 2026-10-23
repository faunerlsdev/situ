using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Geography.ListCountries;

public sealed class ListCountriesHandler(IApplicationDbContext db)
    : IHandler<ListCountriesRequest, Result<ListCountriesResponse>>
{
    public async Task<Result<ListCountriesResponse>> Handle(
        ListCountriesRequest request,
        CancellationToken cancellationToken)
    {
        var countries = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsEnabled)
            .OrderBy(country => country.Name)
            .Select(country => new CountryItem(
                country.Id,
                country.IsoCode,
                country.Name,
                country.RegionLabel,
                country.DefaultLocale,
                country.DefaultCurrency,
                country.DefaultTimeZone,
                country.DistanceUnit,
                country.MinimumAge,
                country.IsEnabled))
            .ToListAsync(cancellationToken);

        return Result<ListCountriesResponse>.Success(new ListCountriesResponse(countries));
    }
}
