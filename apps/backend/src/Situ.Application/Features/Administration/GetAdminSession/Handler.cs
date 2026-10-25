using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Moderation;
using Situ.Domain.Reports;
using Situ.Domain.Retention;

namespace Situ.Application.Features.Administration.GetAdminSession;

public sealed record GetAdminMeRequest(Guid ActorId);

public sealed record AdminRoleView(Guid Id, string Name);

public sealed record GetAdminMeResponse(Guid UserId, string UserName, IReadOnlyList<AdminRoleView> Roles, IReadOnlyList<string> Permissions);

public sealed class GetAdminMeHandler(IApplicationDbContext db)
    : IHandler<GetAdminMeRequest, Result<GetAdminMeResponse>>
{
    public async Task<Result<GetAdminMeResponse>> Handle(GetAdminMeRequest request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty)
        {
            return Result<GetAdminMeResponse>.Failure(AdminSupport.Unauthenticated());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ActorId, cancellationToken);
        if (user is null)
        {
            return Result<GetAdminMeResponse>.Failure(AdminSupport.Forbidden());
        }

        var rows = await (
            from assignment in db.StaffAssignments
            join role in db.StaffRoles on assignment.RoleId equals role.Id
            join grant in db.StaffPermissions on role.Id equals grant.RoleId
            where assignment.UserId == request.ActorId && assignment.IsActive
            select new { role.Id, role.Name, grant.Code }).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Result<GetAdminMeResponse>.Failure(AdminSupport.Forbidden());
        }

        var roles = rows
            .DistinctBy(row => row.Id)
            .Select(row => new AdminRoleView(row.Id, row.Name))
            .ToList();
        var permissions = rows.Select(row => row.Code).Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToList();
        return Result<GetAdminMeResponse>.Success(new GetAdminMeResponse(user.Id, user.UserName.Value, roles, permissions));
    }
}

public sealed record GetAdminDashboardRequest(Guid ActorId);

public sealed record CategoryCountView(string Code, int Count);

public sealed record GetAdminDashboardResponse(
    int UsersTotal,
    int UsersActiveIn7Days,
    int ReportsActive,
    IReadOnlyList<CategoryCountView> ReportsByCategoryIn7Days,
    int ComplaintsOpen,
    int ComplaintsPriority,
    int TakedownsOverdue,
    int TakedownsDueSoon,
    int DeletionRequestsPending);

public sealed class GetAdminDashboardHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetAdminDashboardRequest, Result<GetAdminDashboardResponse>>
{
    public async Task<Result<GetAdminDashboardResponse>> Handle(GetAdminDashboardRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, Situ.Domain.Administration.StaffPermissionCodes.MetricsView, cancellationToken) is { } denied)
        {
            return Result<GetAdminDashboardResponse>.Failure(denied);
        }

        var now = clock.UtcNow;
        var since = now.AddDays(-7);
        var soon = now.AddDays(2);
        var users = await db.Users.CountAsync(cancellationToken);
        var activeUsers = await db.Users.CountAsync(item => item.LastLoginAt >= since, cancellationToken);
        var reports = await db.Reports.CountAsync(item => item.Status == ReportStatus.Active && item.ExpiresAt > now, cancellationToken);
        var recent = await db.Reports
            .Where(item => item.CreatedAt >= since)
            .GroupBy(item => item.CategoryId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var categories = await db.ReportCategories.Select(item => new { item.Id, item.Code }).ToListAsync(cancellationToken);
        var byCode = recent
            .Select(item => new CategoryCountView(categories.FirstOrDefault(category => category.Id == item.Key)?.Code ?? "unknown", item.Count))
            .OrderByDescending(item => item.Count)
            .ToList();
        var open = await db.Complaints.CountAsync(item => item.Status == ComplaintStatus.Open || item.Status == ComplaintStatus.InReview, cancellationToken);
        var priority = await db.Complaints.CountAsync(
            item => item.IsPriority && (item.Status == ComplaintStatus.Open || item.Status == ComplaintStatus.InReview),
            cancellationToken);
        var overdue = await db.TakedownRequests.CountAsync(
            item => (item.Status == TakedownStatus.Received || item.Status == TakedownStatus.UnderReview) && item.DueAt < now,
            cancellationToken);
        var dueSoon = await db.TakedownRequests.CountAsync(
            item => (item.Status == TakedownStatus.Received || item.Status == TakedownStatus.UnderReview) && item.DueAt >= now && item.DueAt <= soon,
            cancellationToken);
        var deletions = await db.AccountDeletionRequests.CountAsync(item => item.Status == AccountDeletionStatus.Scheduled, cancellationToken);
        return Result<GetAdminDashboardResponse>.Success(new GetAdminDashboardResponse(
            users,
            activeUsers,
            reports,
            byCode,
            open,
            priority,
            overdue,
            dueSoon,
            deletions));
    }
}
