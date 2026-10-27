using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Notifications;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.Monetization;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Features.Administration.Users;

public sealed record SearchAdminUsersRequest(Guid ActorId, string Query);

public sealed record AdminUserSummary(Guid Id, string UserName, string Email, string Status, bool IsMinor);

public sealed record SearchAdminUsersResponse(IReadOnlyList<AdminUserSummary> Users);

public sealed class SearchAdminUsersValidator : AbstractValidator<SearchAdminUsersRequest>
{
    public SearchAdminUsersValidator()
    {
        RuleFor(request => request.Query).MinimumLength(3).MaximumLength(254);
    }
}

public sealed class SearchAdminUsersHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SearchAdminUsersRequest, Result<SearchAdminUsersResponse>>
{
    public async Task<Result<SearchAdminUsersResponse>> Handle(SearchAdminUsersRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.UsersView, cancellationToken) is { } denied)
        {
            return Result<SearchAdminUsersResponse>.Failure(denied);
        }

        var query = request.Query.Trim();
        List<User> matches;
        if (query.Contains('@', StringComparison.Ordinal))
        {
            if (!Email.TryCreate(query, out var email))
            {
                return Result<SearchAdminUsersResponse>.Success(new SearchAdminUsersResponse([]));
            }

            matches = await db.Users.Where(item => item.Email == email).ToListAsync(cancellationToken);
        }
        else
        {
            if (!UserName.TryCreate(query, out var userName))
            {
                return Result<SearchAdminUsersResponse>.Success(new SearchAdminUsersResponse([]));
            }

            matches = await db.Users.Where(item => item.UserName == userName).ToListAsync(cancellationToken);
        }

        var now = clock.UtcNow;
        var views = matches.Select(item => new AdminUserSummary(
            item.Id,
            item.UserName.Value,
            AdminSupport.MaskEmail(item.Email.Value),
            item.Status.ToString(),
            item.IsMinor(now))).ToList();
        return Result<SearchAdminUsersResponse>.Success(new SearchAdminUsersResponse(views));
    }
}

public sealed record GetAdminUserRequest(Guid ActorId, string? IpAddress, Guid UserId, bool RevealEmail, bool RevealPhone = false);

public sealed record AdminUserDetail(
    Guid Id,
    string UserName,
    string Email,
    bool EmailRevealed,
    string Status,
    bool IsMinor,
    int MemberSinceYear,
    int MemberSinceMonth,
    string PlanCode,
    int DeviceCount,
    int ReportCount,
    int ComplaintsReceived,
    int SanctionCount,
    string? MaskedPhone,
    int FriendCount,
    string ProfileVisibility);

public sealed class GetAdminUserHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetAdminUserRequest, Result<AdminUserDetail>>
{
    public async Task<Result<AdminUserDetail>> Handle(GetAdminUserRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.UsersView, cancellationToken) is { } denied)
        {
            return Result<AdminUserDetail>.Failure(denied);
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AdminUserDetail>.Failure(Error.NotFound("user.not_found", "No encontramos la cuenta."));
        }

        var now = clock.UtcNow;
        var reveal = request.RevealEmail && await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.UsersViewEmail, cancellationToken);
        var revealPhone = request.RevealPhone && await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.UsersViewEmail, cancellationToken);
        string? maskedPhone = null;
        if (reveal)
        {
            AdminSupport.Record(db, request.ActorId, "users.view_email", nameof(User), user.Id.ToString(), AdminSupport.Json(new { emailRevealed = true }), request.IpAddress, now);
        }

        if (revealPhone && user.Phone is { } phone)
        {
            maskedPhone = phone.Mask();
            AdminSupport.Record(db, request.ActorId, "users.view_phone", nameof(User), user.Id.ToString(), AdminSupport.Json(new { phoneRevealed = true }), request.IpAddress, now);
        }

        if (reveal || maskedPhone is not null)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var plan = await PlanCodeAsync(user.Id, now, cancellationToken);
        var devices = await db.UserDevices.CountAsync(item => item.UserId == user.Id && item.RevokedAt == null, cancellationToken);
        var reports = await db.Reports.CountAsync(item => item.AuthorId == user.Id && !item.IsAnonymous, cancellationToken);
        var publicReportIds = db.Reports.Where(item => item.AuthorId == user.Id && !item.IsAnonymous).Select(item => item.Id);
        var publicCommentIds = db.Comments.Where(item => item.AuthorId == user.Id && !item.IsAnonymous).Select(item => item.Id);
        var complaints = await db.Complaints.CountAsync(
            item => (item.TargetType == ContentTargetType.User && item.TargetId == user.Id)
                || (item.TargetType == ContentTargetType.Report && publicReportIds.Contains(item.TargetId))
                || (item.TargetType == ContentTargetType.Comment && publicCommentIds.Contains(item.TargetId)),
            cancellationToken);
        var sanctions = await db.ModerationActions.CountAsync(item => item.TargetType == ContentTargetType.User && item.TargetId == user.Id, cancellationToken);
        var friends = await db.Friendships.CountAsync(
            item => item.Status == FriendshipStatus.Accepted && (item.RequesterId == user.Id || item.AddresseeId == user.Id),
            cancellationToken);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        return Result<AdminUserDetail>.Success(new AdminUserDetail(
            user.Id,
            user.UserName.Value,
            reveal ? user.Email.Value : AdminSupport.MaskEmail(user.Email.Value),
            reveal,
            user.Status.ToString(),
            user.IsMinor(now),
            user.CreatedAt.Year,
            user.CreatedAt.Month,
            plan,
            devices,
            reports,
            complaints,
            sanctions,
            maskedPhone,
            friends,
            profile?.Visibility == ProfileVisibility.Public ? "Public" : "Private"));
    }

    private async Task<string> PlanCodeAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions
            .Where(item => item.UserId == userId && (item.Status == SubscriptionStatus.Active || item.Status == SubscriptionStatus.PastDue) && item.CurrentPeriodEnd > now)
            .OrderByDescending(item => item.CurrentPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
        if (subscription is not null)
        {
            var paid = await db.SubscriptionPlans.Where(item => item.Id == subscription.PlanId).Select(item => item.Code).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(paid))
            {
                return paid;
            }
        }

        return await db.SubscriptionPlans.Where(item => item.Tier == PlanTier.Free).Select(item => item.Code).FirstOrDefaultAsync(cancellationToken) ?? "free";
    }
}

public sealed record SanctionUserRequest(Guid ActorId, string? IpAddress, Guid UserId, string Action, string? Reason, DateTimeOffset? Until);

public sealed class SanctionUserValidator : AbstractValidator<SanctionUserRequest>
{
    public SanctionUserValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(request => request.Action).NotEmpty();
    }
}

public sealed class SanctionUserHandler(IApplicationDbContext db, IClock clock) : IHandler<SanctionUserRequest, Result>
{
    public async Task<Result> Handle(SanctionUserRequest request, CancellationToken cancellationToken)
    {
        var action = request.Action switch
        {
            "warn" => ModerationActionType.WarnUser,
            "suspend" => ModerationActionType.SuspendUser,
            "ban" => ModerationActionType.BanUser,
            "reinstate" => ModerationActionType.ReinstateUser,
            "force-logout" => ModerationActionType.ForceLogout,
            _ => (ModerationActionType?)null,
        };
        if (action is null)
        {
            return Result.Failure(Error.Validation("sanction.unknown", "Esa sanción no existe."));
        }

        Action<User>? change = action switch
        {
            ModerationActionType.SuspendUser => user => user.Suspend(request.Until),
            ModerationActionType.BanUser => user => user.Ban(),
            ModerationActionType.ReinstateUser => user => user.Reinstate(clock.UtcNow),
            _ => null,
        };
        var result = await Sanction.Apply(db, clock, request, change, action.Value, cancellationToken);
        if (result.IsFailure || action != ModerationActionType.ForceLogout)
        {
            return result;
        }

        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens.Where(item => item.UserId == request.UserId && item.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        var devices = await db.UserDevices.Where(item => item.UserId == request.UserId && item.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            device.Revoke(now);
        }

        await DeviceAlerts.ForgetDevicesAsync(db, devices.Select(item => item.Id).ToArray(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class Sanction
{
    public static async Task<Result> Apply(
        IApplicationDbContext db,
        IClock clock,
        SanctionUserRequest request,
        Action<User>? change,
        ModerationActionType action,
        CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.UsersSuspend, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure(Error.Validation("sanction.reason_required", "Escribe el motivo de la sanción."));
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "No encontramos la cuenta."));
        }

        var now = clock.UtcNow;
        if (request.Until is { } until && until <= now)
        {
            return Result.Failure(Error.Validation("sanction.until_invalid", "La suspensión tiene que terminar en el futuro."));
        }

        var before = user.Status.ToString();
        change?.Invoke(user);
        db.ModerationActions.Add(ModerationAction.Record(
            request.ActorId,
            action,
            ContentTargetType.User,
            user.Id,
            null,
            request.Reason.Trim(),
            now));
        AdminSupport.Record(
            db,
            request.ActorId,
            "user." + action.ToString().ToLowerInvariant(),
            nameof(User),
            user.Id.ToString(),
            AdminSupport.Json(new { before, after = user.Status.ToString(), until = request.Until }),
            request.IpAddress,
            now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
