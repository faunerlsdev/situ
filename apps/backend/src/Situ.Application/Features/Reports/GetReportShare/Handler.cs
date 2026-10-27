using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.GetReportShare;

public sealed record GetReportShareRequest(Guid ReportId);

public sealed record GetReportShareResponse(bool Indexable, string? CategoryName, string? Zone);

public sealed class GetReportShareHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetReportShareRequest, Result<GetReportShareResponse>>
{
    public async Task<Result<GetReportShareResponse>> Handle(GetReportShareRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var report = await db.Reports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.ReportId, cancellationToken);
        if (report is null || !CanIndex(report, now))
        {
            return Result<GetReportShareResponse>.Success(new GetReportShareResponse(false, null, null));
        }

        var author = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == report.AuthorId, cancellationToken);
        if (author is null || author.IsMinor(now))
        {
            return Result<GetReportShareResponse>.Success(new GetReportShareResponse(false, null, null));
        }

        var category = await db.ReportCategories.AsNoTracking().FirstOrDefaultAsync(item => item.Id == report.CategoryId, cancellationToken);
        string? zone = null;
        if (report.CityId is { } cityId)
        {
            zone = await db.Cities.AsNoTracking().Where(item => item.Id == cityId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        }

        zone ??= ApproximateZone(report.Location.Latitude, report.Location.Longitude);
        return Result<GetReportShareResponse>.Success(new GetReportShareResponse(true, category?.Name ?? "Reporte", zone));
    }

    internal static bool CanIndex(Report report, DateTimeOffset now) =>
        report.Visibility == ContentVisibility.Public && !report.AuthorIsMinor && report.IsVisible(now);

    internal static string ApproximateZone(double latitude, double longitude)
    {
        var lat = Math.Round(latitude, 1, MidpointRounding.AwayFromZero);
        var lon = Math.Round(longitude, 1, MidpointRounding.AwayFromZero);
        return FormattableString.Invariant($"Zona aproximada {lat:0.0}, {lon:0.0}");
    }
}
