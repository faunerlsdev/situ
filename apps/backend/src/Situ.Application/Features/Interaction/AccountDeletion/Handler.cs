using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Location;
using Situ.Domain.Administration;
using Situ.Domain.Social;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Reports;
using Situ.Domain.Retention;

namespace Situ.Application.Features.Interaction.AccountDeletion;

public sealed record RequestAccountDeletionRequest(Guid UserId, string Password);

public sealed record RequestAccountDeletionResponse(DateTimeOffset ScheduledFor);

public sealed class RequestAccountDeletionValidator : AbstractValidator<RequestAccountDeletionRequest>
{
    public RequestAccountDeletionValidator()
    {
        RuleFor(request => request.Password).NotEmpty();
    }
}

public sealed class RequestAccountDeletionHandler(
    IApplicationDbContext db,
    IClock clock,
    IPasswordHasher passwords,
    IEmailSender email)
    : IHandler<RequestAccountDeletionRequest, Result<RequestAccountDeletionResponse>>
{
    public async Task<Result<RequestAccountDeletionResponse>> Handle(RequestAccountDeletionRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null || user.Status is UserStatus.Deleted)
        {
            return Result<RequestAccountDeletionResponse>.Failure(InteractionErrors.PasswordInvalid());
        }

        if (!passwords.Verify(request.Password, user.PasswordHash))
        {
            return Result<RequestAccountDeletionResponse>.Failure(InteractionErrors.PasswordInvalid());
        }

        var open = await db.AccountDeletionRequests.AnyAsync(
            item => item.UserId == user.Id && item.Status == AccountDeletionStatus.Scheduled,
            cancellationToken);
        if (open || user.Status == UserStatus.DeletionRequested)
        {
            return Result<RequestAccountDeletionResponse>.Failure(InteractionErrors.DeletionAlreadyScheduled());
        }

        var now = clock.UtcNow;
        var settings = await Reports.ReportSettings.LoadAsync(db, cancellationToken);
        var days = Math.Max(1, Reports.ReportSettings.Int(settings, SystemSettingKeys.AccountDeletionGraceDays, 14));
        user.RequestDeletion(now);
        var deletion = AccountDeletionRequest.Create(user.Id, now, TimeSpan.FromDays(days));
        db.AccountDeletionRequests.Add(deletion);

        var devices = await db.UserDevices.Where(item => item.UserId == user.Id && item.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            device.Revoke(now);
        }

        await Notifications.DeviceAlerts.ForgetDevicesAsync(db, devices.Select(item => item.Id).ToArray(), cancellationToken);

        var tokens = await db.RefreshTokens.Where(item => item.UserId == user.Id && item.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        Audit.Write(db, user.Id, "account_deletion.requested", "User", user.Id, deletion.ScheduledFor.ToString("O"), now);
        await db.SaveChangesAsync(cancellationToken);
        await email.SendAsync(
            user.Email.Value,
            "SITU: solicitud para eliminar tu cuenta",
            $"Recibimos la solicitud. Si no la cancelas, eliminaremos la cuenta el {deletion.ScheduledFor:yyyy-MM-dd}. Para cancelarla, entra de nuevo y elige cancelar la eliminación antes de esa fecha.",
            cancellationToken);
        return Result<RequestAccountDeletionResponse>.Success(new RequestAccountDeletionResponse(deletion.ScheduledFor));
    }
}

public sealed record CancelAccountDeletionRequest(Guid UserId);

public sealed class CancelAccountDeletionHandler(IApplicationDbContext db, IClock clock)
    : IHandler<CancelAccountDeletionRequest, Result>
{
    public async Task<Result> Handle(CancelAccountDeletionRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var deletion = await db.AccountDeletionRequests
            .Where(item => item.UserId == request.UserId && item.Status == AccountDeletionStatus.Scheduled)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null || deletion is null)
        {
            return Result.Failure(InteractionErrors.DeletionNotScheduled());
        }

        try
        {
            deletion.Cancel(now);
        }
        catch (DomainException)
        {
            return Result.Failure(InteractionErrors.DeletionNotScheduled());
        }

        user.CancelDeletion(now);
        Audit.Write(db, user.Id, "account_deletion.cancelled", "User", user.Id, null, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record CompleteAccountDeletionsRequest;

public sealed record CompleteAccountDeletionsResponse(int Completed, int BlockedByLegalHold);

public sealed class CompleteAccountDeletionsHandler(IApplicationDbContext db, IClock clock, IFileStorage files, IFriendLocationPublisher? locations = null)
    : IHandler<CompleteAccountDeletionsRequest, Result<CompleteAccountDeletionsResponse>>
{
    public async Task<Result<CompleteAccountDeletionsResponse>> Handle(CompleteAccountDeletionsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await db.AccountDeletionRequests
            .Where(item => item.Status == AccountDeletionStatus.Scheduled && item.ScheduledFor <= now)
            .ToListAsync(cancellationToken);
        var completed = 0;
        var blockedCount = 0;
        foreach (var deletion in due)
        {
            var reportIdsForHold = await db.Reports.Where(item => item.AuthorId == deletion.UserId).Select(item => item.Id).ToListAsync(cancellationToken);
            var commentIdsForHold = await db.Comments.Where(item => item.AuthorId == deletion.UserId).Select(item => item.Id).ToListAsync(cancellationToken);
            var holds = await db.LegalHolds.Where(item => item.ReleasedAt == null).Select(item => new { item.TargetType, item.TargetId }).ToListAsync(cancellationToken);
            var held = holds.Any(item =>
                (item.TargetType == ContentTargetType.User && item.TargetId == deletion.UserId)
                || (item.TargetType == ContentTargetType.Report && reportIdsForHold.Contains(item.TargetId))
                || (item.TargetType == ContentTargetType.Comment && commentIdsForHold.Contains(item.TargetId)));
            if (held)
            {
                deletion.BlockByLegalHold(now);
                Audit.Write(db, null, "account_deletion.blocked_by_legal_hold", "User", deletion.UserId, deletion.Id.ToString(), now);
                blockedCount++;
                continue;
            }

            var ties = await db.Friendships
                .Where(item => item.Status == FriendshipStatus.Accepted && (item.RequesterId == deletion.UserId || item.AddresseeId == deletion.UserId))
                .ToListAsync(cancellationToken);
            var friendIds = ties.Select(item => item.OtherParty(deletion.UserId)).ToArray();
            await EraseAsync(deletion.UserId, now, cancellationToken);
            await LocationRules.RemoveFromAsync(locations, friendIds, deletion.UserId, cancellationToken);
            deletion.Complete(now);
            Audit.Write(db, null, "account_deletion.completed", "User", deletion.UserId, null, now);
            completed++;
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<CompleteAccountDeletionsResponse>.Success(new CompleteAccountDeletionsResponse(completed, blockedCount));
    }

    private async Task EraseAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return;
        }

        var email = user.Email.Value;
        var reportIds = await db.Reports.Where(item => item.AuthorId == userId).Select(item => item.Id).ToListAsync(cancellationToken);
        var commentIds = await db.Comments.Where(item => item.AuthorId == userId || reportIds.Contains(item.TargetId)).Select(item => item.Id).ToListAsync(cancellationToken);
        var ownReactions = await db.Reactions
            .Where(item => item.UserId == userId && item.TargetType == ContentTargetType.Report && !reportIds.Contains(item.TargetId))
            .Select(item => item.TargetId)
            .ToListAsync(cancellationToken);
        foreach (var reportId in ownReactions.Distinct())
        {
            var host = await db.Reports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
            if (host is null)
            {
                continue;
            }

            var removed = ownReactions.Count(item => item == reportId);
            host.SetReactionCount(Math.Max(0, host.ReactionCount - removed));
        }

        var media = await db.ReportMedias.Where(item => item.OwnerUserId == userId || (item.ReportId != null && reportIds.Contains(item.ReportId.Value))).ToListAsync(cancellationToken);
        foreach (var file in media)
        {
            await files.DeleteAsync(file.StorageKey, cancellationToken);
            if (file.ThumbnailKey is not null)
            {
                await files.DeleteAsync(file.ThumbnailKey, cancellationToken);
            }
        }

        db.ReportMedias.RemoveRange(media);
        var foreignComments = await db.Comments.Where(item => item.AuthorId == userId && item.TargetType == ContentTargetType.Report && !reportIds.Contains(item.TargetId) && item.Status == CommentStatus.Visible).ToListAsync(cancellationToken);
        foreach (var comment in foreignComments)
        {
            var host = await db.Reports.FirstOrDefaultAsync(item => item.Id == comment.TargetId, cancellationToken);
            host?.RemoveComment();
        }

        db.Comments.RemoveRange(await db.Comments.Where(item => commentIds.Contains(item.Id) || item.AuthorId == userId).ToListAsync(cancellationToken));
        db.Reactions.RemoveRange(await db.Reactions.Where(item => item.UserId == userId || (item.TargetType == ContentTargetType.Report && reportIds.Contains(item.TargetId)) || (item.TargetType == ContentTargetType.Comment && commentIds.Contains(item.TargetId))).ToListAsync(cancellationToken));
        db.ReportConfirmations.RemoveRange(await db.ReportConfirmations.Where(item => item.UserId == userId || reportIds.Contains(item.ReportId)).ToListAsync(cancellationToken));
        db.Reports.RemoveRange(await db.Reports.Where(item => reportIds.Contains(item.Id)).ToListAsync(cancellationToken));
        db.AnonymousParticipants.RemoveRange(await db.AnonymousParticipants.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.Friendships.RemoveRange(await db.Friendships.Where(item => item.RequesterId == userId || item.AddresseeId == userId).ToListAsync(cancellationToken));
        db.FriendInvitations.RemoveRange(await db.FriendInvitations.Where(item => item.InviterId == userId || item.Email == email).ToListAsync(cancellationToken));
        db.LocationShareRecipients.RemoveRange(await db.LocationShareRecipients.Where(item => item.UserId == userId || item.FriendUserId == userId).ToListAsync(cancellationToken));
        db.UserLocations.RemoveRange(await db.UserLocations.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.PrivacyZones.RemoveRange(await db.PrivacyZones.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.UserBlocks.RemoveRange(await db.UserBlocks.Where(item => item.BlockerId == userId || item.BlockedUserId == userId).ToListAsync(cancellationToken));
        db.StaffAssignments.RemoveRange(await db.StaffAssignments.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        var notificationIds = await db.Notifications.Where(item => item.RecipientId == userId).Select(item => item.Id).ToListAsync(cancellationToken);
        db.NotificationDeliveries.RemoveRange(await db.NotificationDeliveries.Where(item => notificationIds.Contains(item.NotificationId)).ToListAsync(cancellationToken));
        db.Notifications.RemoveRange(await db.Notifications.Where(item => notificationIds.Contains(item.Id)).ToListAsync(cancellationToken));
        db.NotificationPreferences.RemoveRange(await db.NotificationPreferences.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.NotificationSettings.RemoveRange(await db.NotificationSettings.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.UserCategoryInterests.RemoveRange(await db.UserCategoryInterests.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.UserInterestCities.RemoveRange(await db.UserInterestCities.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.DeviceAlertLocations.RemoveRange(await db.DeviceAlertLocations.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.RefreshTokens.RemoveRange(await db.RefreshTokens.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.SecurityTokens.RemoveRange(await db.SecurityTokens.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.UserDevices.RemoveRange(await db.UserDevices.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        db.LoginAttempts.RemoveRange(await db.LoginAttempts.Where(item => item.UserId == userId || item.EmailAttempted == email).ToListAsync(cancellationToken));
        db.DataExportRequests.RemoveRange(await db.DataExportRequests.Where(item => item.UserId == userId).ToListAsync(cancellationToken));
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        profile?.Erase(now);
        user.CompleteDeletion(now);
    }
}
