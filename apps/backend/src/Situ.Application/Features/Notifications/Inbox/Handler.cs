using System.Globalization;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Notifications;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Notifications.Inbox;

public sealed record ListNotificationsRequest(Guid UserId, string? Cursor, int Limit = 20);
public sealed record NotificationItemResponse(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? TargetType,
    Guid? TargetId,
    Guid? ReportId,
    string? LiveId,
    DateTimeOffset CreatedAt,
    bool IsRead,
    bool Available);
public sealed record ListNotificationsResponse(IReadOnlyList<NotificationItemResponse> Notifications, string? NextCursor);
public sealed record UnreadCountRequest(Guid UserId);
public sealed record UnreadCountResponse(int Count);
public sealed record MarkNotificationReadRequest(Guid UserId, Guid NotificationId);
public sealed record MarkAllNotificationsReadRequest(Guid UserId);

public sealed class ListNotificationsValidator : AbstractValidator<ListNotificationsRequest>
{
    public ListNotificationsValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 50);
    }
}

public sealed class ListNotificationsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListNotificationsRequest, Result<ListNotificationsResponse>>
{
    public async Task<Result<ListNotificationsResponse>> Handle(ListNotificationsRequest request, CancellationToken cancellationToken)
    {
        var query = db.Notifications.Where(item => item.RecipientId == request.UserId);
        if (NotificationCursor.TryDecode(request.Cursor, out var createdAt, out var id))
        {
            query = query.Where(item => item.CreatedAt < createdAt || (item.CreatedAt == createdAt && item.Id.CompareTo(id) < 0));
        }

        var page = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(request.Limit + 1).ToListAsync(cancellationToken);
        var extra = page.Count > request.Limit;
        if (extra)
        {
            page.RemoveAt(page.Count - 1);
        }

        var now = clock.UtcNow;
        var reportIds = page.Where(item => item.TargetType == ContentTargetType.Report && item.TargetId != null).Select(item => item.TargetId!.Value).ToList();
        var commentIds = page.Where(item => item.TargetType == ContentTargetType.Comment && item.TargetId != null).Select(item => item.TargetId!.Value).ToArray();
        var comments = await db.Comments.Where(item => commentIds.Contains(item.Id)).Select(item => new { item.Id, item.TargetId }).ToListAsync(cancellationToken);
        reportIds.AddRange(comments.Select(item => item.TargetId));
        var reports = await db.Reports.Where(item => reportIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var liveIds = page.Where(item => item.TargetType == ContentTargetType.LiveStream && item.TargetId != null).Select(item => item.TargetId!.Value).ToArray();
        var lives = await db.LiveStreams.Where(item => liveIds.Contains(item.Id)).Select(item => new { item.Id, item.PublicPath, item.HiddenByStaff }).ToListAsync(cancellationToken);
        var items = page.Select(item =>
        {
            var reportId = item.TargetType == ContentTargetType.Report
                ? item.TargetId
                : comments.FirstOrDefault(comment => comment.Id == item.TargetId)?.TargetId;
            var report = reportId is { } found ? reports.FirstOrDefault(row => row.Id == found) : null;
            var live = item.TargetType == ContentTargetType.LiveStream ? lives.FirstOrDefault(row => row.Id == item.TargetId) : null;
            var available = item.TargetType == ContentTargetType.LiveStream
                ? live is not null && !live.HiddenByStaff
                : report is not null && report.IsVisible(now);
            return new NotificationItemResponse(
                item.Id,
                item.Type.ToString(),
                item.Title,
                item.Body,
                item.TargetType?.ToString(),
                item.TargetId,
                reportId,
                live?.PublicPath,
                item.CreatedAt,
                item.IsRead,
                available);
        }).ToList();
        var next = extra && page.Count > 0 ? NotificationCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;
        return Result<ListNotificationsResponse>.Success(new ListNotificationsResponse(items, next));
    }
}

public sealed class UnreadCountHandler(IApplicationDbContext db)
    : IHandler<UnreadCountRequest, Result<UnreadCountResponse>>
{
    public async Task<Result<UnreadCountResponse>> Handle(UnreadCountRequest request, CancellationToken cancellationToken)
    {
        var count = await db.Notifications.CountAsync(item => item.RecipientId == request.UserId && item.ReadAt == null, cancellationToken);
        return Result<UnreadCountResponse>.Success(new UnreadCountResponse(count));
    }
}

public sealed class MarkNotificationReadHandler(IApplicationDbContext db, IClock clock)
    : IHandler<MarkNotificationReadRequest, Result>
{
    public async Task<Result> Handle(MarkNotificationReadRequest request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(item => item.Id == request.NotificationId && item.RecipientId == request.UserId, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(NotificationErrors.NotFound());
        }

        notification.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class MarkAllNotificationsReadHandler(IApplicationDbContext db, IClock clock)
    : IHandler<MarkAllNotificationsReadRequest, Result>
{
    public async Task<Result> Handle(MarkAllNotificationsReadRequest request, CancellationToken cancellationToken)
    {
        var unread = await db.Notifications.Where(item => item.RecipientId == request.UserId && item.ReadAt == null).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class NotificationCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{createdAt.UtcTicks}:{id:N}")));

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var split = text.Split(':');
            if (split.Length != 2 || !long.TryParse(split[0], out var ticks) || !Guid.TryParse(split[1], out id))
            {
                return false;
            }

            createdAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
