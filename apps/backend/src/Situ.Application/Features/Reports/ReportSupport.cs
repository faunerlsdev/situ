using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Anonymity;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Features.Reports;

internal static class ReportErrors
{
    public static Error EmailUnconfirmed() =>
        Error.Forbidden("report.email_unconfirmed", "Confirma tu correo para publicar.");

    public static Error PendingGuardian() =>
        Error.Forbidden("report.pending_guardian", "Tu cuenta espera la autorización de un acudiente.");

    public static Error CannotPublish() =>
        Error.Forbidden("report.cannot_publish", "Tu cuenta no puede publicar.");

    public static Error MinorPublicIdentity() =>
        Error.Forbidden(
            "report.minor_public_identity",
            "Si eres menor de edad, un reporte público tiene que ser anónimo. También puedes publicarlo solo para amigos.");

    public static Error CategoryUnavailable() =>
        Error.Validation("report.category_unavailable", "Esa categoría no está disponible.");

    public static Error AdultOnly() =>
        Error.Forbidden("report.adult_only", "Esta categoría es solo para mayores de edad.");

    public static Error MediaRequired() =>
        Error.Validation("report.media_required", "Esta categoría pide al menos una foto o un video.");

    public static Error NotFound() =>
        Error.NotFound("report.not_found", "No encontramos el reporte.");

    public static Error NotAvailable(string reportStatus) =>
        new(
            "report.not_available",
            "Este reporte ya no está disponible.",
            ErrorType.NotFound,
            new Dictionary<string, object?> { ["reportStatus"] = reportStatus });

    public static Error NotAuthor() =>
        Error.Forbidden("report.not_author", "Solo quien creó el reporte puede cambiarlo.");

    public static Error ConfirmationOwn() =>
        Error.Forbidden("report.confirmation_own", "No puedes confirmar tu propio reporte.");

    public static Error ConfirmationTooFar() =>
        Error.Validation("report.confirmation_too_far", "Tienes que estar cerca del lugar para confirmar el reporte.");

    public static Error DailyLimit(DateTimeOffset retryAfter) =>
        Error.RateLimited("report.daily_limit", "Llegaste al límite de reportes de tu plan por hoy.", retryAfter);

    public static Error AnonymousDailyLimit(DateTimeOffset retryAfter) =>
        Error.RateLimited("report.anonymous_daily_limit", "Llegaste al límite de publicaciones anónimas de tu plan por hoy.", retryAfter);

    public static Error StaffForbidden() =>
        Error.Forbidden("category.forbidden", "No tienes permiso para administrar categorías.");

    public static Error CategoryNotFound() =>
        Error.NotFound("category.not_found", "No encontramos la categoría.");

    public static Error CodeTaken() =>
        Error.Conflict("category.code_taken", "Ya existe una categoría con ese código.");

    public static Error MediaNotFound() =>
        Error.NotFound("report.media_not_found", "No encontramos el archivo.");

    public static Error FromDomain(DomainException exception) =>
        Error.Validation(exception.Code, "Revisa los datos del reporte.");
}

internal static class ReportSettings
{
    public static async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var keys = SystemSettingKeys.Defaults.Keys.ToArray();
        var stored = await db.SystemSettings
            .Where(setting => keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);
        var merged = new Dictionary<string, string>(SystemSettingKeys.Defaults);
        foreach (var (key, value) in stored)
        {
            merged[key] = value;
        }

        return merged;
    }

    public static int Int(IReadOnlyDictionary<string, string> settings, string key, int fallback) =>
        int.TryParse(settings.GetValueOrDefault(key), out var value) ? value : fallback;

    public static long Long(IReadOnlyDictionary<string, string> settings, string key, long fallback) =>
        long.TryParse(settings.GetValueOrDefault(key), out var value) ? value : fallback;

    public static string DisplayName(IReadOnlyDictionary<string, string> settings) =>
        settings.GetValueOrDefault(SystemSettingKeys.AnonymousDisplayName) ?? AnonymousIdentity.DefaultDisplayName;
}

internal static class CatalogVersions
{
    public static async Task<int> ReadAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var value = await db.SystemSettings
            .Where(setting => setting.Key == SystemSettingKeys.ReportCatalogVersion)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return int.TryParse(value, out var version) ? version : 1;
    }

    public static async Task BumpAsync(IApplicationDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(item => item.Key == SystemSettingKeys.ReportCatalogVersion, cancellationToken);
        if (setting is null)
        {
            db.SystemSettings.Add(SystemSetting.Create(
                SystemSettingKeys.ReportCatalogVersion,
                "2",
                "Versión del catálogo de categorías.",
                now));
            return;
        }

        var next = int.TryParse(setting.Value, out var current) ? current + 1 : 2;
        setting.ChangeValue(next.ToString(), now);
    }
}

internal static class StaffGate
{
    public static async Task<bool> HasAsync(
        IApplicationDbContext db,
        Guid userId,
        string permission,
        CancellationToken cancellationToken) =>
        await (
            from assignment in db.StaffAssignments
            join grant in db.StaffPermissions on assignment.RoleId equals grant.RoleId
            where assignment.UserId == userId && assignment.IsActive && grant.Code == permission
            select grant.Id).AnyAsync(cancellationToken);

    public static async Task<string?> RoleAsync(
        IApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var active = await db.StaffAssignments.AnyAsync(
            assignment => assignment.UserId == userId && assignment.IsActive,
            cancellationToken);
        return active ? "Staff" : null;
    }
}

/// <summary>Full hides every post. Anonymous hides only anonymous posts of those authors, and only for the blocker.</summary>
internal sealed record ContentBlocks(IReadOnlySet<Guid> Full, IReadOnlySet<Guid> Anonymous)
{
    public static ContentBlocks None { get; } = new(new HashSet<Guid>(), new HashSet<Guid>());

    public bool Hides(Guid authorId, bool anonymous) =>
        Full.Contains(authorId) || (anonymous && Anonymous.Contains(authorId));
}

internal static class ReportAccess
{
    public static async Task<HashSet<Guid>> FriendIdsAsync(
        IApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Friendships
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted
                && (friendship.RequesterId == userId || friendship.AddresseeId == userId))
            .Select(friendship => new { friendship.RequesterId, friendship.AddresseeId })
            .ToListAsync(cancellationToken);
        return rows.Select(row => row.RequesterId == userId ? row.AddresseeId : row.RequesterId).ToHashSet();
    }

    public static async Task<ContentBlocks> BlocksAsync(
        IApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await db.UserBlocks
            .Where(block => block.BlockerId == userId || (block.Scope == UserBlockScope.Full && block.BlockedUserId == userId))
            .Select(block => new { block.BlockerId, block.BlockedUserId, block.Scope })
            .ToListAsync(cancellationToken);
        var full = new HashSet<Guid>();
        var anonymous = new HashSet<Guid>();
        foreach (var row in rows)
        {
            if (row.Scope == UserBlockScope.Full)
            {
                full.Add(row.BlockerId == userId ? row.BlockedUserId : row.BlockerId);
                continue;
            }

            if (row.BlockerId == userId)
            {
                anonymous.Add(row.BlockedUserId);
            }
        }

        return new ContentBlocks(full, anonymous);
    }

    public static async Task<IReadOnlySet<Guid>> BlockedIdsAsync(
        IApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken) =>
        (await BlocksAsync(db, userId, cancellationToken)).Full;

    public static bool CanSee(Report report, Guid? viewerId, IReadOnlySet<Guid> friends, ContentBlocks blocks)
    {
        if (viewerId != report.AuthorId && blocks.Hides(report.AuthorId, report.IsAnonymous))
        {
            return false;
        }

        if (report.Visibility == ContentVisibility.Public)
        {
            return true;
        }

        return viewerId is { } user && (report.AuthorId == user || friends.Contains(report.AuthorId));
    }
}

/// <summary>
/// Single place that turns a stored author into the public shape.
/// Anonymous reports never expose the user id, the avatar or a profile link.
/// </summary>
public static class ReportPresentation
{
    public static ReportAuthorView Author(
        bool isAnonymous,
        User? user,
        UserProfile? profile,
        string anonymousName)
    {
        if (isAnonymous)
        {
            var since = user is null ? default : AnonymousIdentity.MemberSince(user.CreatedAt);
            return new ReportAuthorView(
                null,
                anonymousName,
                null,
                user is null ? null : since.Year,
                user is null ? null : since.Month,
                true,
                true);
        }

        return new ReportAuthorView(
            user?.Id,
            profile?.DisplayName ?? user?.UserName.Value ?? "Usuario",
            profile?.AvatarUrl,
            null,
            null,
            false,
            false);
    }

    public static string ReadUrl(Guid mediaId, long expiresUnix, string signature) =>
        $"/api/v1/reports/media/{mediaId}/content?exp={expiresUnix}&sig={signature}";
}

public sealed record ReportAuthorView(
    Guid? UserId,
    string DisplayName,
    string? AvatarUrl,
    int? MemberSinceYear,
    int? MemberSinceMonth,
    bool IsAnonymous,
    bool HideAuthorOnMap);

public sealed record ReportMediaView(Guid Id, string Type, string Url, string? ThumbnailUrl, int? DurationSeconds);

public sealed record ReactionCountView(string Type, int Count);

public sealed record ReportCommentView(
    Guid Id,
    string Text,
    ReportAuthorView Author,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReactionCountView> ReactionCounts,
    string? MyReaction);

public sealed record ReportCardView(
    Guid Id,
    Guid CategoryId,
    string? CategoryCode,
    string Description,
    double Latitude,
    double Longitude,
    string Visibility,
    bool IsAnonymous,
    bool HideAuthorOnMap,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    int StillThereCount,
    int GoneNowCount,
    int CommentCount,
    int ReactionCount,
    ReportAuthorView Author,
    string? PreviewUrl,
    IReadOnlyList<ReactionCountView> ReactionCounts,
    string? MyReaction);
