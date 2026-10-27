using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Anonymity;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.News;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Features.Interaction;

internal static class InteractionErrors
{
    public static Error EmailUnconfirmed() =>
        Error.Forbidden("comment.email_unconfirmed", "Confirma tu correo para comentar.");

    public static Error PendingGuardian() =>
        Error.Forbidden("comment.pending_guardian", "Tu cuenta espera la autorización de un acudiente.");

    public static Error CannotPublish() =>
        Error.Forbidden("comment.cannot_publish", "Tu cuenta no puede comentar.");

    public static Error MinorPublicIdentity() =>
        Error.Forbidden("comment.minor_public_identity", "Si eres menor de edad, en un reporte público el comentario tiene que ser anónimo. En un reporte de solo amigos puedes usar tu nombre.");

    public static Error ReportClosed() =>
        Error.Validation("comment.report_closed", "Este reporte ya no admite comentarios.");

    public static Error NotFound() =>
        Error.NotFound("comment.not_found", "No encontramos el comentario.");

    public static Error NotAuthor() =>
        Error.Forbidden("comment.not_author", "Solo quien escribió el comentario puede cambiarlo.");

    public static Error ParentInvalid() =>
        Error.Validation("comment.parent_invalid", "Esa respuesta no se puede publicar ahí.");

    public static Error AnonymousDailyLimit(DateTimeOffset retryAfter) =>
        Error.RateLimited("comment.anonymous_daily_limit", "Llegaste al límite de comentarios anónimos de hoy.", retryAfter);

    public static Error ReactionNotFound() =>
        Error.NotFound("reaction.not_found", "No encontramos esa reacción.");

    public static Error ComplaintNotFound() =>
        Error.NotFound("complaint.not_found", "No encontramos la denuncia.");

    public static Error ComplaintDuplicate() =>
        Error.Conflict("complaint.duplicate", "Ya tienes una denuncia abierta sobre eso.");

    public static Error ComplaintSelf() =>
        Error.Validation("complaint.self", "No puedes denunciarte a ti mismo ni tu propio contenido.");

    public static Error ComplaintTarget() =>
        Error.NotFound("complaint.target_not_found", "No encontramos lo que quieres denunciar.");

    public static Error StaffForbidden() =>
        Error.Forbidden("complaint.forbidden", "No tienes permiso para moderar denuncias.");

    public static Error SuspendForbidden() =>
        Error.Forbidden("users.suspend_forbidden", "No tienes permiso para suspender cuentas.");

    public static Error RevealForbidden() =>
        Error.Forbidden("anonymity.forbidden", "No tienes permiso para ver la identidad anónima.");

    public static Error RevealDenied() =>
        Error.Forbidden("anonymity.reveal_denied", "Hace falta una denuncia de ese contenido o una justificación válida.");

    public static Error RevealLogFailed() =>
        Error.Failure("anonymity.log_failed", "No se pudo registrar el acceso. No revelamos la identidad.");

    public static Error BlockSelf() =>
        Error.Validation("block.self", "No puedes bloquearte a ti mismo.");

    public static Error BlockNotFound() =>
        Error.NotFound("block.not_found", "No encontramos ese bloqueo.");

    public static Error PasswordInvalid() =>
        Error.Unauthorized("account.password_invalid", "La contraseña no coincide.");

    public static Error DeletionAlreadyScheduled() =>
        Error.Conflict("account_deletion.already_scheduled", "Ya hay una solicitud de eliminación en curso.");

    public static Error DeletionNotScheduled() =>
        Error.Validation("account_deletion.not_scheduled", "No hay una eliminación que cancelar.");
}

internal static class InteractionAccess
{
    public static Error? PublishBlock(User user)
    {
        if (user.Status == UserStatus.PendingGuardianConsent)
        {
            return InteractionErrors.PendingGuardian();
        }

        if (user.EmailConfirmedAt is null)
        {
            return InteractionErrors.EmailUnconfirmed();
        }

        if (!user.CanPublish)
        {
            return InteractionErrors.CannotPublish();
        }

        return null;
    }

    public static async Task<Result<Report>> OpenReportAsync(
        IApplicationDbContext db,
        Guid? viewerId,
        Guid reportId,
        bool requireActive,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null)
        {
            return Result<Report>.Failure(ReportErrors.NotFound());
        }

        var friends = viewerId is { } viewer ? await ReportAccess.FriendIdsAsync(db, viewer, cancellationToken) : new HashSet<Guid>();
        var blocks = viewerId is { } blockedViewer ? await ReportAccess.BlocksAsync(db, blockedViewer, cancellationToken) : ContentBlocks.None;
        var isAuthor = viewerId == report.AuthorId;
        if (!isAuthor && !ReportAccess.CanSee(report, viewerId, friends, blocks))
        {
            return Result<Report>.Failure(ReportErrors.NotFound());
        }

        if (!isAuthor && report.Status != ReportStatus.Active)
        {
            return Result<Report>.Failure(ReportErrors.NotFound());
        }

        if (requireActive && report.Status != ReportStatus.Active)
        {
            return Result<Report>.Failure(InteractionErrors.ReportClosed());
        }

        if (requireActive && now >= report.ExpiresAt)
        {
            return Result<Report>.Failure(InteractionErrors.ReportClosed());
        }

        return Result<Report>.Success(report);
    }

    public static async Task<bool> HidesAuthorAsync(
        IApplicationDbContext db,
        Guid viewerId,
        Guid authorId,
        bool anonymous,
        CancellationToken cancellationToken)
    {
        var blocks = await ReportAccess.BlocksAsync(db, viewerId, cancellationToken);
        return blocks.Hides(authorId, anonymous);
    }

    public static async Task SeverRelationshipAsync(
        IApplicationDbContext db,
        Guid left,
        Guid right,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var friendships = await db.Friendships
            .Where(item =>
                (item.RequesterId == left && item.AddresseeId == right)
                || (item.RequesterId == right && item.AddresseeId == left))
            .ToListAsync(cancellationToken);
        foreach (var friendship in friendships)
        {
            friendship.Sever(now);
        }

        var shares = await db.LocationShareRecipients
            .Where(item =>
                (item.UserId == left && item.FriendUserId == right)
                || (item.UserId == right && item.FriendUserId == left))
            .ToListAsync(cancellationToken);
        db.LocationShareRecipients.RemoveRange(shares);

        // The pair stops here: the friendship is no longer accepted and these rows are gone.
        // The global "share with friends" switch stays on so other friends are not cut off.
        // The next location channel must read accepted friendships and these rows, and stop
        // pushing this pair as soon as they disappear. There is no live channel yet.
    }
}

internal static class AnonymousIndexAllocator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public static async Task<int> AssignAsync(
        IApplicationDbContext db,
        ContentTargetType targetType,
        Guid targetId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd($"{targetType}:{targetId}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var existing = await db.AnonymousParticipants.FirstOrDefaultAsync(
                    item => item.TargetType == targetType && item.TargetId == targetId && item.UserId == userId,
                    cancellationToken);
                if (existing is not null)
                {
                    return existing.Index;
                }

                var max = await db.AnonymousParticipants
                    .Where(item => item.TargetType == targetType && item.TargetId == targetId)
                    .Select(item => (int?)item.Index)
                    .MaxAsync(cancellationToken) ?? 0;
                var created = AnonymousParticipant.Assign(targetType, targetId, userId, max + 1, now);
                db.AnonymousParticipants.Add(created);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    return created.Index;
                }
                catch (DbUpdateException) when (attempt < 2)
                {
                    db.AnonymousParticipants.Remove(created);
                }
            }

            throw new InvalidOperationException("Could not reserve an anonymous participant index.");
        }
        finally
        {
            gate.Release();
        }
    }
}

internal static class ReactionViews
{
    public static async Task<IReadOnlyDictionary<Guid, (IReadOnlyList<ReactionCountView> Counts, string? Mine)>> ForAsync(
        IApplicationDbContext db,
        ContentTargetType targetType,
        IReadOnlyCollection<Guid> targetIds,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        if (targetIds.Count == 0)
        {
            return new Dictionary<Guid, (IReadOnlyList<ReactionCountView> Counts, string? Mine)>();
        }

        var blocks = viewerId is { } viewer ? await ReportAccess.BlocksAsync(db, viewer, cancellationToken) : ContentBlocks.None;
        var rows = await db.Reactions
            .Where(item => item.TargetType == targetType && targetIds.Contains(item.TargetId))
            .Select(item => new { item.TargetId, item.UserId, item.Type })
            .ToListAsync(cancellationToken);
        rows = rows
            .Where(item => viewerId == item.UserId || !blocks.Hides(item.UserId, anonymous: true))
            .ToList();
        return rows
            .GroupBy(item => item.TargetId)
            .ToDictionary(
                group => group.Key,
                group => (
                    (IReadOnlyList<ReactionCountView>)group
                        .GroupBy(item => item.Type)
                        .OrderBy(item => item.Key.ToString(), StringComparer.Ordinal)
                        .Select(item => new ReactionCountView(item.Key.ToString(), item.Count()))
                        .ToList(),
                    viewerId is { } viewer
                        ? group.FirstOrDefault(item => item.UserId == viewer)?.Type.ToString()
                        : null));
    }

    public static async Task SyncReportCountAsync(
        IApplicationDbContext db,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null)
        {
            return;
        }

        var count = await db.Reactions.CountAsync(
            item => item.TargetType == ContentTargetType.Report && item.TargetId == reportId,
            cancellationToken);
        report.SetReactionCount(count);
    }
}

internal static class CommentPresentation
{
    public static ReportAuthorView Author(Comment comment, User? user, UserProfile? profile, string baseName) =>
        ReportPresentation.Author(
            comment.IsAnonymous,
            comment.IsAnonymous ? user : user,
            comment.IsAnonymous ? null : profile,
            comment.IsAnonymous ? AnonymousIdentity.DisplayName(comment.AnonymousIndex, baseName) : baseName);
}

internal static class ComplaintEffects
{
    public static async Task ApplyAutomaticReviewAsync(
        IApplicationDbContext db,
        Complaint complaint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var threshold = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.ComplaintAutoReviewThreshold, 3));
        var distinct = await db.Complaints
            .Where(item => item.TargetType == complaint.TargetType
                && item.TargetId == complaint.TargetId
                && item.Status != ComplaintStatus.Dismissed)
            .Select(item => item.ReporterId)
            .Distinct()
            .CountAsync(cancellationToken);
        if (!complaint.IsPriority && distinct < threshold)
        {
            return;
        }

        if (complaint.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
            report?.SendToReview(now);
            return;
        }

        if (complaint.TargetType == ContentTargetType.Comment)
        {
            var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
            if (comment is { Status: CommentStatus.Visible })
            {
                comment.Hide(now);
                if (comment.TargetType == ContentTargetType.Report)
                {
                    var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == comment.TargetId, cancellationToken);
                    report?.RemoveComment();
                }
            }
        }
    }

    public static async Task<bool> StillNeedsReviewAsync(
        IApplicationDbContext db,
        ContentTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var threshold = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.ComplaintAutoReviewThreshold, 3));
        var open = await db.Complaints
            .Where(item => item.TargetType == targetType
                && item.TargetId == targetId
                && item.Status != ComplaintStatus.Dismissed)
            .Select(item => new { item.ReporterId, item.IsPriority })
            .ToListAsync(cancellationToken);
        return open.Any(item => item.IsPriority) || open.Select(item => item.ReporterId).Distinct().Count() >= threshold;
    }

    public static async Task PenalizeAuthorAsync(
        IApplicationDbContext db,
        Guid authorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reputation = await db.UserReputations.FirstOrDefaultAsync(item => item.UserId == authorId, cancellationToken);
        if (reputation is null)
        {
            reputation = UserReputation.CreateDefault(authorId, now);
            db.UserReputations.Add(reputation);
        }

        reputation.PenalizeRejectedReport(now);
    }
}

internal static class Audit
{
    public static void Write(
        IApplicationDbContext db,
        Guid? actorId,
        string action,
        string entityType,
        Guid? entityId,
        string? changes,
        DateTimeOffset now)
    {
        db.AuditLogs.Add(AuditLog.Record(actorId, action, entityType, entityId?.ToString(), changes, null, now));
    }
}
