using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Domain.Common;
using Situ.Infrastructure.Persistence;

namespace Situ.Infrastructure.Notifications;

public sealed class PostGisAlertLocationQuery(AppDbContext db) : IAlertLocationQuery
{
    public async Task<IReadOnlyList<ProximityMatch>> MatchAsync(GeoPoint origin, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var latitude = origin.Latitude;
        var longitude = origin.Longitude;
        var rows = await db.Database.SqlQuery<ProximityRow>($"""
            SELECT DISTINCT ON (d.user_id)
                   d.user_id AS "UserId",
                   ST_Distance(d.location, ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography) AS "DistanceMeters"
            FROM device_alert_locations AS d
            LEFT JOIN notification_settings AS s ON s.user_id = d.user_id
            WHERE d.expires_at > {now}
              AND COALESCE(s.notify_nearby_reports, TRUE)
              AND ST_DWithin(
                    d.location,
                    ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography,
                    COALESCE(s.nearby_radius_meters, 1000))
            ORDER BY d.user_id, ST_Distance(d.location, ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography)
            """).ToListAsync(cancellationToken);
        return rows.Select(row => new ProximityMatch(row.UserId, row.DistanceMeters)).ToList();
    }

    public async Task<IReadOnlyList<ProximityMatch>> MatchWithinAsync(GeoPoint origin, int radiusMeters, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var latitude = origin.Latitude;
        var longitude = origin.Longitude;
        var rows = await db.Database.SqlQuery<ProximityRow>($"""
            SELECT DISTINCT ON (d.user_id)
                   d.user_id AS "UserId",
                   ST_Distance(d.location, ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography) AS "DistanceMeters"
            FROM device_alert_locations AS d
            WHERE d.expires_at > {now}
              AND ST_DWithin(
                    d.location,
                    ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography,
                    {radiusMeters})
            ORDER BY d.user_id, ST_Distance(d.location, ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography)
            """).ToListAsync(cancellationToken);
        return rows.Select(row => new ProximityMatch(row.UserId, row.DistanceMeters)).ToList();
    }

    private sealed class ProximityRow
    {
        public Guid UserId { get; set; }
        public double DistanceMeters { get; set; }
    }
}
