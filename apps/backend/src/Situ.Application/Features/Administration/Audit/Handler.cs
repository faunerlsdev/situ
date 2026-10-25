using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;

namespace Situ.Application.Features.Administration.Audit;

public sealed record ListAuditLogsRequest(Guid ActorId, Guid? ActorFilter, string? Action, string? EntityType, DateTimeOffset? From, DateTimeOffset? To, string? Cursor, int Limit);

public sealed record AuditLogView(Guid Id, DateTimeOffset OccurredAt, Guid? ActorUserId, string Action, string EntityType, string? EntityId, string? Changes, string? IpAddress);

public sealed record ListAuditLogsResponse(IReadOnlyList<AuditLogView> Items, string? NextCursor);

public sealed class ListAuditLogsHandler(IApplicationDbContext db)
    : IHandler<ListAuditLogsRequest, Result<ListAuditLogsResponse>>
{
    public async Task<Result<ListAuditLogsResponse>> Handle(ListAuditLogsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.AuditView, cancellationToken) is { } denied)
        {
            return Result<ListAuditLogsResponse>.Failure(denied);
        }

        var query = db.AuditLogs.AsQueryable();
        if (request.ActorFilter is { } actor)
        {
            query = query.Where(item => item.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(item => item.Action == request.Action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(item => item.EntityType == request.EntityType);
        }

        if (request.From is { } from)
        {
            query = query.Where(item => item.OccurredAt >= from);
        }

        if (request.To is { } to)
        {
            query = query.Where(item => item.OccurredAt <= to);
        }

        if (AdminSupport.TryDecodeCursor(request.Cursor, out var at, out var id))
        {
            query = query.Where(item => item.OccurredAt < at || (item.OccurredAt == at && item.Id.CompareTo(id) < 0));
        }

        var limit = request.Limit is > 0 and <= 100 ? request.Limit : 50;
        var rows = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = rows.Take(limit).Select(item => new AuditLogView(item.Id, item.OccurredAt, item.ActorUserId, item.Action, item.EntityType, item.EntityId, item.Changes, item.IpAddress)).ToList();
        var next = rows.Count > limit ? AdminSupport.EncodeCursor(rows[limit - 1].OccurredAt, rows[limit - 1].Id) : null;
        return Result<ListAuditLogsResponse>.Success(new ListAuditLogsResponse(page, next));
    }
}

public sealed record ListAnonymousAccessLogsRequest(Guid ActorId, string? Cursor, int Limit);

public sealed record AnonymousAccessView(Guid Id, DateTimeOffset CreatedAt, Guid StaffId, string TargetType, Guid TargetId, Guid? ComplaintId, string Justification);

public sealed record ListAnonymousAccessLogsResponse(IReadOnlyList<AnonymousAccessView> Items, string? NextCursor);

public sealed class ListAnonymousAccessLogsHandler(IApplicationDbContext db)
    : IHandler<ListAnonymousAccessLogsRequest, Result<ListAnonymousAccessLogsResponse>>
{
    public async Task<Result<ListAnonymousAccessLogsResponse>> Handle(ListAnonymousAccessLogsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.AuditView, cancellationToken) is { } denied)
        {
            return Result<ListAnonymousAccessLogsResponse>.Failure(denied);
        }

        var query = db.AnonymousIdentityAccessLogs.AsQueryable();
        if (AdminSupport.TryDecodeCursor(request.Cursor, out var at, out var id))
        {
            query = query.Where(item => item.CreatedAt < at || (item.CreatedAt == at && item.Id.CompareTo(id) < 0));
        }

        var limit = request.Limit is > 0 and <= 100 ? request.Limit : 50;
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = rows.Take(limit).Select(item => new AnonymousAccessView(item.Id, item.CreatedAt, item.StaffId, item.TargetType.ToString(), item.TargetId, item.ComplaintId, item.Justification)).ToList();
        var next = rows.Count > limit ? AdminSupport.EncodeCursor(rows[limit - 1].CreatedAt, rows[limit - 1].Id) : null;
        return Result<ListAnonymousAccessLogsResponse>.Success(new ListAnonymousAccessLogsResponse(page, next));
    }
}
