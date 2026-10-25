using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.GetReportMap;

public sealed record GetReportMapRequest(
    Guid ViewerId,
    double MinLatitude,
    double MinLongitude,
    double MaxLatitude,
    double MaxLongitude,
    int Zoom,
    IReadOnlyList<string> Categories,
    string? Since,
    string? Type,
    double? UserLatitude,
    double? UserLongitude);

public sealed record MapItemView(
    string Kind,
    Guid? Id,
    int Count,
    double Latitude,
    double Longitude,
    Guid? CategoryId,
    string? CategoryCode,
    DateTimeOffset? CreatedAt,
    double? DistanceMeters,
    ReportAuthorView? Author,
    int StillThereCount,
    int GoneNowCount,
    int CommentCount,
    string? PreviewUrl,
    bool IsAnonymous,
    bool HideAuthorOnMap,
    bool IsMine);

public sealed record GetReportMapResponse(IReadOnlyList<MapItemView> Items, int Limit);

public sealed record MapQuery(
    Guid ViewerId,
    double MinLatitude,
    double MinLongitude,
    double MaxLatitude,
    double MaxLongitude,
    int Zoom,
    IReadOnlyList<Guid> CategoryIds,
    DateTimeOffset? Since,
    bool FriendsOnly,
    bool ViewerIsMinor,
    IReadOnlySet<Guid> FriendIds,
    IReadOnlySet<Guid> BlockedIds,
    IReadOnlySet<Guid> AnonymousBlockedIds);

public sealed record MapRow(
    bool Cluster,
    Guid? ReportId,
    int Count,
    double Latitude,
    double Longitude,
    Guid? CategoryId,
    string? CategoryCode,
    DateTimeOffset? CreatedAt,
    Guid? AuthorId,
    bool IsAnonymous,
    int StillThereCount,
    int GoneNowCount,
    int CommentCount,
    Guid? PreviewMediaId,
    string? Description);

public interface IReportMapReader
{
    Task<IReadOnlyList<MapRow>> ReadAsync(MapQuery query, CancellationToken cancellationToken);

    Task<string?> ExplainAsync(MapQuery query, CancellationToken cancellationToken);
}

public sealed class GetReportMapHandler(IApplicationDbContext db, IClock clock, IReportMapReader maps, IMediaUrlSigner urls)
    : IHandler<GetReportMapRequest, Result<GetReportMapResponse>>
{
    public const int ItemLimit = 500;

    public async Task<Result<GetReportMapResponse>> Handle(GetReportMapRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ViewerId, cancellationToken);
        if (user is null)
        {
            return Result<GetReportMapResponse>.Failure(ReportErrors.NotFound());
        }

        var categoryIds = await ResolveCategoriesAsync(request.Categories, cancellationToken);
        DateTimeOffset? since = request.Since?.ToLowerInvariant() switch
        {
            "hour" => now.AddHours(-1),
            "today" => new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero),
            _ => null,
        };
        var blocks = await ReportAccess.BlocksAsync(db, request.ViewerId, cancellationToken);
        var query = new MapQuery(
            request.ViewerId,
            request.MinLatitude,
            request.MinLongitude,
            request.MaxLatitude,
            request.MaxLongitude,
            request.Zoom,
            categoryIds,
            since,
            string.Equals(request.Type, "friends", StringComparison.OrdinalIgnoreCase),
            user.IsMinor(now),
            await ReportAccess.FriendIdsAsync(db, request.ViewerId, cancellationToken),
            blocks.Full,
            blocks.Anonymous);
        var rows = await maps.ReadAsync(query, cancellationToken);
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var name = ReportSettings.DisplayName(settings);
        var authorIds = rows.Where(row => row.AuthorId is not null && !row.IsAnonymous).Select(row => row.AuthorId!.Value).Distinct().ToArray();
        var users = await db.Users.Where(item => authorIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => authorIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var anonymousUsers = rows.Where(row => row.IsAnonymous && row.AuthorId is not null).Select(row => row.AuthorId!.Value).Distinct().ToArray();
        var anonymous = await db.Users.Where(item => anonymousUsers.Contains(item.Id)).ToListAsync(cancellationToken);
        double? userLat = request.UserLatitude;
        double? userLon = request.UserLongitude;
        var items = new List<MapItemView>();
        foreach (var row in rows.Take(ItemLimit))
        {
            string? preview = null;
            if (row.PreviewMediaId is { } mediaId)
            {
                var signed = urls.Sign(mediaId, now.AddMinutes(15));
                preview = ReportPresentation.ReadUrl(mediaId, signed.ExpiresUnix, signed.Signature);
            }

            ReportAuthorView? author = null;
            if (!row.Cluster && row.AuthorId is { } authorId)
            {
                if (row.IsAnonymous)
                {
                    author = ReportPresentation.Author(true, anonymous.FirstOrDefault(item => item.Id == authorId), null, name);
                }
                else
                {
                    author = ReportPresentation.Author(false, users.FirstOrDefault(item => item.Id == authorId), profiles.FirstOrDefault(item => item.UserId == authorId), name);
                }
            }

            double? distance = null;
            if (userLat is { } lat && userLon is { } lon && !row.Cluster)
            {
                distance = new GeoPoint(lat, lon).DistanceInMetersTo(new GeoPoint(row.Latitude, row.Longitude));
            }

            items.Add(new MapItemView(
                row.Cluster ? "cluster" : "point",
                row.Cluster ? null : row.ReportId,
                row.Count,
                row.Latitude,
                row.Longitude,
                row.CategoryId,
                row.CategoryCode,
                row.CreatedAt,
                distance,
                author,
                row.StillThereCount,
                row.GoneNowCount,
                row.CommentCount,
                preview,
                row.IsAnonymous,
                row.IsAnonymous,
                !row.Cluster && row.AuthorId == request.ViewerId));
        }

        return Result<GetReportMapResponse>.Success(new GetReportMapResponse(items, ItemLimit));
    }

    private async Task<IReadOnlyList<Guid>> ResolveCategoriesAsync(IReadOnlyList<string> categories, CancellationToken cancellationToken)
    {
        if (categories.Count == 0)
        {
            return [];
        }

        var ids = new List<Guid>();
        var codes = new List<string>();
        foreach (var category in categories)
        {
            if (Guid.TryParse(category, out var id))
            {
                ids.Add(id);
            }
            else if (!string.IsNullOrWhiteSpace(category))
            {
                codes.Add(category.Trim().ToLowerInvariant());
            }
        }

        if (codes.Count > 0)
        {
            var matched = await db.ReportCategories.Where(item => codes.Contains(item.Code)).Select(item => item.Id).ToListAsync(cancellationToken);
            ids.AddRange(matched);
        }

        return ids;
    }
}
