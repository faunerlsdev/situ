using Situ.Domain.Common;

namespace Situ.Domain.Reports;

public enum MapZoneNoticeStatus
{
    Pending,
    Published,
}

/// <summary>
/// Outbox row for one map change. It stores the cell of the report, never a viewer's location,
/// and never the description or the author.
/// </summary>
public sealed class MapZoneNotice : AuditableEntity
{
    public const string Created = "ReportCreated";
    public const string Updated = ReportMapChanged.Updated;
    public const string Resolved = ReportMapChanged.Resolved;
    public const string Expired = ReportMapChanged.Expired;
    public const string Removed = ReportMapChanged.Removed;

    private MapZoneNotice()
    {
        Type = null!;
        CellId = null!;
    }

    public string Type { get; private set; }
    public Guid ReportId { get; private set; }
    public string CellId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public MapZoneNoticeStatus Status { get; private set; }

    public static bool IsKnown(string? type) => type is Created or Updated or Resolved or Expired or Removed;

    public static MapZoneNotice Enqueue(string type, Guid reportId, string cellId, DateTimeOffset now)
    {
        if (!IsKnown(type))
        {
            throw new DomainException("map.unknown_event", "Unknown map zone event.");
        }

        var notice = new MapZoneNotice
        {
            Type = type,
            ReportId = Guard.NotEmpty(reportId, nameof(reportId)),
            CellId = Guard.NotEmpty(cellId, nameof(cellId), 32),
            OccurredAt = now,
            Status = MapZoneNoticeStatus.Pending,
        };
        notice.MarkCreated(now);
        return notice;
    }

    public void MarkPublished(DateTimeOffset now)
    {
        if (Status == MapZoneNoticeStatus.Published)
        {
            return;
        }

        Status = MapZoneNoticeStatus.Published;
        MarkUpdated(now);
    }

    public static int Priority(string type) => type switch
    {
        Removed => 5,
        Expired => 4,
        Resolved => 3,
        Updated => 2,
        Created => 1,
        _ => 0,
    };

    /// <summary>One signal per report inside each cell. Removal wins over a create in the same batch.</summary>
    public static IReadOnlyList<MapZoneBatch> Coalesce(IEnumerable<MapZoneNotice> notices)
    {
        return notices
            .GroupBy(notice => notice.CellId, StringComparer.Ordinal)
            .Select(group =>
            {
                var winners = group
                    .GroupBy(notice => notice.ReportId)
                    .Select(reports => reports
                        .OrderByDescending(notice => Priority(notice.Type))
                        .ThenByDescending(notice => notice.OccurredAt)
                        .First())
                    .OrderBy(notice => notice.OccurredAt)
                    .ToArray();
                return new MapZoneBatch(group.Key, winners);
            })
            .ToArray();
    }
}

public sealed record MapZoneBatch(string CellId, IReadOnlyList<MapZoneNotice> Notices);
