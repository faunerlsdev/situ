using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.GetPublicReportMap;

public sealed record GetPublicReportMapRequest(double MinLat, double MinLon, double MaxLat, double MaxLon);

/// <summary>
/// A pin without author, text, media or counts. Coordinates are rounded to 3 decimal degrees,
/// about 111 m (0.001° of latitude is ~111.32 m). The time is the hour, not the exact minute.
/// </summary>
public sealed record PublicMapPin(
    string CategoryCode,
    string CategoryName,
    string ApproximateTime,
    string Zone,
    double Latitude,
    double Longitude);

public sealed record GetPublicReportMapResponse(IReadOnlyList<PublicMapPin> Items);

public static class PublicReadLimits
{
    public const int MaxPins = 50;
    public const int MapPermitsPerMinute = 30;
    public const int SharePermitsPerMinute = 60;

    /// <summary>3 decimal degrees. At the equator this is about 111 m.</summary>
    public static double RoundHundredMeters(double degrees) =>
        Math.Round(degrees, 3, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Anonymous map. It never reads the caller, so a token cannot widen it.
/// Only public reports by adults from the last 24 hours, at most 50 pins.
/// Hidden reports are omitted; the response does not say whether any exist.
/// </summary>
public sealed class GetPublicReportMapHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetPublicReportMapRequest, Result<GetPublicReportMapResponse>>
{
    public async Task<Result<GetPublicReportMapResponse>> Handle(GetPublicReportMapRequest request, CancellationToken cancellationToken)
    {
        if (request.MinLat is < -90 or > 90 || request.MaxLat is < -90 or > 90
            || request.MinLon is < -180 or > 180 || request.MaxLon is < -180 or > 180
            || request.MinLat > request.MaxLat || request.MinLon > request.MaxLon)
        {
            return Result<GetPublicReportMapResponse>.Success(new GetPublicReportMapResponse([]));
        }

        var now = clock.UtcNow;
        var since = now.AddHours(-24);
        var reports = await db.Reports.AsNoTracking()
            .Where(item => item.Visibility == ContentVisibility.Public
                && !item.AuthorIsMinor
                && item.Status == ReportStatus.Active
                && item.CreatedAt >= since
                && item.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        var inside = reports
            .Where(item => item.Location.Latitude >= request.MinLat && item.Location.Latitude <= request.MaxLat
                && item.Location.Longitude >= request.MinLon && item.Location.Longitude <= request.MaxLon)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        if (inside.Count == 0)
        {
            return Result<GetPublicReportMapResponse>.Success(new GetPublicReportMapResponse([]));
        }

        var authorIds = inside.Select(item => item.AuthorId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking()
            .Where(item => authorIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var adultAuthors = authors
            .Where(item => !item.IsMinor(now) && item.Status != UserStatus.Deleted)
            .Select(item => item.Id)
            .ToHashSet();

        var categoryIds = inside.Select(item => item.CategoryId).Distinct().ToList();
        var categories = await db.ReportCategories.AsNoTracking()
            .Where(item => categoryIds.Contains(item.Id) && item.IsActive)
            .ToListAsync(cancellationToken);
        var categoryById = categories.ToDictionary(item => item.Id);

        var cityIds = inside.Where(item => item.CityId != null).Select(item => item.CityId!.Value).Distinct().ToList();
        var cities = await db.Cities.AsNoTracking()
            .Where(item => cityIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var cityById = cities.ToDictionary(item => item.Id);

        var overrides = await db.ReportCategoryOverrides.AsNoTracking()
            .Where(item => categoryIds.Contains(item.CategoryId))
            .ToListAsync(cancellationToken);

        var pins = new List<PublicMapPin>();
        foreach (var report in inside)
        {
            if (pins.Count == PublicReadLimits.MaxPins)
            {
                break;
            }

            if (!adultAuthors.Contains(report.AuthorId) || !categoryById.TryGetValue(report.CategoryId, out var category))
            {
                continue;
            }

            Guid? countryId = null;
            string? zone = null;
            if (report.CityId is { } cityId && cityById.TryGetValue(cityId, out var city))
            {
                countryId = city.CountryId;
                zone = city.Name;
            }

            if (Disabled(overrides, report.CategoryId, countryId, report.CityId))
            {
                continue;
            }

            var hour = report.CreatedAt.ToUniversalTime();
            pins.Add(new PublicMapPin(
                category.Code,
                category.Name,
                new DateTimeOffset(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, TimeSpan.Zero).ToString("yyyy-MM-ddTHH:00:00Z"),
                string.IsNullOrWhiteSpace(zone) ? "Zona aproximada" : zone,
                PublicReadLimits.RoundHundredMeters(report.Location.Latitude),
                PublicReadLimits.RoundHundredMeters(report.Location.Longitude)));
        }

        return Result<GetPublicReportMapResponse>.Success(new GetPublicReportMapResponse(pins));
    }

    private static bool Disabled(
        IReadOnlyList<ReportCategoryOverride> overrides,
        Guid categoryId,
        Guid? countryId,
        Guid? cityId)
    {
        foreach (var item in overrides)
        {
            if (item.CategoryId != categoryId || item.IsEnabled)
            {
                continue;
            }

            if (cityId is { } city && item.CityId == city)
            {
                return true;
            }

            if (item.CityId is null && countryId is { } country && item.CountryId == country)
            {
                return true;
            }
        }

        return false;
    }
}
