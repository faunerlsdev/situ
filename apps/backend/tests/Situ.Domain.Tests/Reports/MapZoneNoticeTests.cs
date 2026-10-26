using Situ.Domain.Reports;

namespace Situ.Domain.Tests.Reports;

public class MapZoneNoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Coalesce_keeps_one_signal_per_report_and_prefers_removal()
    {
        var report = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var notices = new[]
        {
            MapZoneNotice.Enqueue(MapZoneNotice.Created, report, "-1482:94", Now),
            MapZoneNotice.Enqueue(MapZoneNotice.Updated, report, "-1482:94", Now.AddSeconds(1)),
            MapZoneNotice.Enqueue(MapZoneNotice.Removed, report, "-1482:94", Now.AddSeconds(2)),
            MapZoneNotice.Enqueue(MapZoneNotice.Created, other, "0:0", Now),
            MapZoneNotice.Enqueue(MapZoneNotice.Updated, other, "0:0", Now.AddSeconds(1)),
        };

        var batches = MapZoneNotice.Coalesce(notices);

        var bogota = Assert.Single(batches, batch => batch.CellId == "-1482:94");
        Assert.Equal(MapZoneNotice.Removed, Assert.Single(bogota.Notices).Type);
        var origin = Assert.Single(batches, batch => batch.CellId == "0:0");
        Assert.Equal(MapZoneNotice.Updated, Assert.Single(origin.Notices).Type);
    }

    [Fact]
    public void A_notice_has_no_description_and_no_author()
    {
        var names = typeof(MapZoneNotice).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            ["CellId", "CreatedAt", "Id", "OccurredAt", "ReportId", "Status", "Type", "UpdatedAt"],
            names.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(names, name => name.Contains("Author", StringComparison.Ordinal) || name.Contains("Description", StringComparison.Ordinal));
    }
}
