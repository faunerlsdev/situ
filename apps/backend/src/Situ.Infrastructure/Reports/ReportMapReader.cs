using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Infrastructure.Reports;

public sealed class ReportMapReader(IApplicationDbContext db, IClock clock) : IReportMapReader
{
    public async Task<IReadOnlyList<MapRow>> ReadAsync(MapQuery query, CancellationToken cancellationToken)
    {
        if (db is DbContext context && context.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return await ReadPostGisAsync(context, query, clock.UtcNow, cancellationToken);
        }

        return await ReadMemoryAsync(query, cancellationToken);
    }

    public async Task<string?> ExplainAsync(MapQuery query, CancellationToken cancellationToken)
    {
        if (db is not DbContext context || context.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) != true)
        {
            return null;
        }

        var sql = IndividualSql(query, explain: true, clock.UtcNow);
        var lines = new List<string>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Text;
        foreach (var parameter in sql.Parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            if (dbParameter is NpgsqlParameter npgsql && parameter.Value is Guid[])
            {
                npgsql.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid;
            }

            command.Parameters.Add(dbParameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(reader.GetString(0));
        }

        return string.Join('\n', lines);
    }

    private async Task<IReadOnlyList<MapRow>> ReadMemoryAsync(MapQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var adultIds = query.ViewerIsMinor
            ? await db.ReportCategories.Where(category => category.IsAdultOnly).Select(category => category.Id).ToListAsync(cancellationToken)
            : [];
        var reports = await db.Reports
            .Where(report => report.Status == ReportStatus.Active && report.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        var filtered = reports.Where(report =>
            report.Location.Latitude >= query.MinLatitude
            && report.Location.Latitude <= query.MaxLatitude
            && report.Location.Longitude >= query.MinLongitude
            && report.Location.Longitude <= query.MaxLongitude
            && !adultIds.Contains(report.CategoryId)
            && !query.BlockedIds.Contains(report.AuthorId)
            && !(report.IsAnonymous && query.AnonymousBlockedIds.Contains(report.AuthorId))
            && (query.Since is null || report.CreatedAt >= query.Since)
            && (query.CategoryIds.Count == 0 || query.CategoryIds.Contains(report.CategoryId))
            && (!query.FriendsOnly || report.Visibility == ContentVisibility.FriendsOnly)
            && (report.Visibility == ContentVisibility.Public
                || report.AuthorId == query.ViewerId
                || query.FriendIds.Contains(report.AuthorId)))
            .Take(GetReportMapHandler.ItemLimit)
            .ToList();
        var codes = await db.ReportCategories
            .Where(category => filtered.Select(report => report.CategoryId).Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Code, cancellationToken);
        var cell = CellDegrees(query.Zoom);
        if (cell <= 0)
        {
            return filtered.Select(report => new MapRow(
                false,
                report.Id,
                1,
                report.Location.Latitude,
                report.Location.Longitude,
                report.CategoryId,
                codes.GetValueOrDefault(report.CategoryId),
                report.CreatedAt,
                report.AuthorId,
                report.IsAnonymous,
                report.StillThereCount,
                report.GoneNowCount,
                report.CommentCount,
                null,
                report.Description)).ToList();
        }

        return filtered
            .GroupBy(report => (Lat: Math.Round(report.Location.Latitude / cell), Lon: Math.Round(report.Location.Longitude / cell)))
            .Select(group =>
            {
                var first = group.First();
                var many = group.Count() > 1;
                return new MapRow(
                    many,
                    many ? null : first.Id,
                    group.Count(),
                    group.Average(report => report.Location.Latitude),
                    group.Average(report => report.Location.Longitude),
                    many ? null : first.CategoryId,
                    many ? null : codes.GetValueOrDefault(first.CategoryId),
                    many ? null : first.CreatedAt,
                    many ? null : first.AuthorId,
                    !many && first.IsAnonymous,
                    group.Sum(report => report.StillThereCount),
                    group.Sum(report => report.GoneNowCount),
                    group.Sum(report => report.CommentCount),
                    null,
                    many ? null : first.Description);
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<MapRow>> ReadPostGisAsync(DbContext context, MapQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sql = query.Zoom >= 15 ? IndividualSql(query, explain: false, now) : ClusterSql(query, now);
        var rows = new List<MapRow>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Text;
        foreach (var parameter in sql.Parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            if (dbParameter is NpgsqlParameter npgsql && parameter.Value is Guid[])
            {
                npgsql.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid;
            }

            command.Parameters.Add(dbParameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MapRow(
                reader.GetBoolean(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.GetBoolean(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetGuid(13),
                reader.IsDBNull(14) ? null : reader.GetString(14)));
        }

        return rows;
    }

    private static Sql IndividualSql(MapQuery query, bool explain, DateTimeOffset now)
    {
        var text = """
            SELECT false AS cluster, r.id, 1 AS count,
                   ST_Y(r.location::geometry) AS latitude, ST_X(r.location::geometry) AS longitude,
                   r.category_id, c.code, r.created_at, r.author_id, r.is_anonymous,
                   r.still_there_count, r.gone_now_count, r.comment_count,
                   (SELECT m.id FROM report_media m WHERE m.report_id = r.id ORDER BY m.sort_order LIMIT 1) AS preview_media_id,
                   left(r.description, 80) AS description
            FROM reports r
            JOIN report_categories c ON c.id = r.category_id
            WHERE r.status = 'Active' AND r.expires_at > @now
              AND ST_Intersects(r.location, ST_MakeEnvelope(@minLon, @minLat, @maxLon, @maxLat, 4326)::geography)
              AND (@categoryCount = 0 OR r.category_id = ANY(@categories))
              AND (@since IS NULL OR r.created_at >= @since)
              AND (@friendsOnly = false OR r.visibility = 'FriendsOnly')
              AND (@minor = false OR c.is_adult_only = false)
              AND NOT (r.author_id = ANY(@blocked))
              AND NOT (r.is_anonymous AND r.author_id = ANY(@anonymousBlocked))
              AND (r.visibility = 'Public' OR r.author_id = @viewer OR r.author_id = ANY(@friends))
            LIMIT 500
            """;
        if (explain)
        {
            text = "EXPLAIN " + text;
        }

        return new Sql(text, Parameters(query, now));
    }

    private static Sql ClusterSql(MapQuery query, DateTimeOffset now)
    {
        var text = """
            SELECT (COUNT(*) > 1) AS cluster,
                   CASE WHEN COUNT(*) > 1 THEN NULL::uuid ELSE (array_agg(r.id))[1] END AS id,
                   COUNT(*)::int AS count,
                   ST_Y(ST_Centroid(ST_Collect(r.location::geometry))) AS latitude,
                   ST_X(ST_Centroid(ST_Collect(r.location::geometry))) AS longitude,
                   CASE WHEN COUNT(*) > 1 THEN NULL::uuid ELSE (array_agg(r.category_id))[1] END AS category_id,
                   CASE WHEN COUNT(*) > 1 THEN NULL::text ELSE (array_agg(c.code))[1] END AS code,
                   CASE WHEN COUNT(*) > 1 THEN NULL::timestamptz ELSE (array_agg(r.created_at))[1] END AS created_at,
                   CASE WHEN COUNT(*) > 1 THEN NULL::uuid ELSE (array_agg(r.author_id))[1] END AS author_id,
                   CASE WHEN COUNT(*) > 1 THEN false ELSE (array_agg(r.is_anonymous))[1] END AS is_anonymous,
                   SUM(r.still_there_count)::int, SUM(r.gone_now_count)::int, SUM(r.comment_count)::int,
                   NULL::uuid AS preview_media_id, NULL::text AS description
            FROM reports r
            JOIN report_categories c ON c.id = r.category_id
            WHERE r.status = 'Active' AND r.expires_at > @now
              AND ST_Intersects(r.location, ST_MakeEnvelope(@minLon, @minLat, @maxLon, @maxLat, 4326)::geography)
              AND (@categoryCount = 0 OR r.category_id = ANY(@categories))
              AND (@since IS NULL OR r.created_at >= @since)
              AND (@friendsOnly = false OR r.visibility = 'FriendsOnly')
              AND (@minor = false OR c.is_adult_only = false)
              AND NOT (r.author_id = ANY(@blocked))
              AND NOT (r.is_anonymous AND r.author_id = ANY(@anonymousBlocked))
              AND (r.visibility = 'Public' OR r.author_id = @viewer OR r.author_id = ANY(@friends))
            GROUP BY ST_SnapToGrid(r.location::geometry, @cell)
            LIMIT 500
            """;
        var parameters = Parameters(query, now);
        parameters.Add(new SqlParameter("@cell", CellDegrees(query.Zoom)));
        return new Sql(text, parameters);
    }

    private static List<SqlParameter> Parameters(MapQuery query, DateTimeOffset now)
    {
        var categories = query.CategoryIds.Count == 0 ? [Guid.Empty] : query.CategoryIds.ToArray();
        var friends = query.FriendIds.Count == 0 ? [Guid.Empty] : query.FriendIds.ToArray();
        var blocked = query.BlockedIds.Count == 0 ? [Guid.Empty] : query.BlockedIds.ToArray();
        var anonymousBlocked = query.AnonymousBlockedIds.Count == 0 ? [Guid.Empty] : query.AnonymousBlockedIds.ToArray();
        return
        [
            new("@now", now),
            new("@minLat", query.MinLatitude),
            new("@minLon", query.MinLongitude),
            new("@maxLat", query.MaxLatitude),
            new("@maxLon", query.MaxLongitude),
            new("@viewer", query.ViewerId),
            new("@minor", query.ViewerIsMinor),
            new("@friendsOnly", query.FriendsOnly),
            new("@since", query.Since),
            new("@categoryCount", query.CategoryIds.Count),
            new("@categories", categories),
            new("@friends", friends),
            new("@blocked", blocked),
            new("@anonymousBlocked", anonymousBlocked),
        ];
    }

    internal static double CellDegrees(int zoom) =>
        zoom >= 15 ? 0 : 360d / Math.Pow(2, Math.Clamp(zoom, 1, 14));

    private sealed record Sql(string Text, List<SqlParameter> Parameters);

    private sealed record SqlParameter(string Name, object? Value);
}
