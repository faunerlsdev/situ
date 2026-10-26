using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>
/// Zone events are only for reports anyone can already see on the public map.
/// Friends-only reports and reports by minors stay on the ordinary REST map.
/// </summary>
public static class MapZonePolicy
{
    public static bool EmitsZoneEvent(Report report) =>
        report.Visibility == ContentVisibility.Public && !report.AuthorIsMinor;
}
