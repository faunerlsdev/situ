using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Domain.Profiles;
using Situ.Domain.Social;

namespace Situ.Application.Features.Social;

internal static class SocialRules
{
    public const string HiddenProfile = "No encontramos ese perfil.";
    public const string Sent = "sent";

    public static async Task<bool> AllowAdultMinorAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        return string.Equals(settings.GetValueOrDefault(SystemSettingKeys.FriendsAllowAdultMinor), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<IReadOnlyDictionary<string, string>> SettingsAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await ReportSettings.LoadAsync(db, cancellationToken);

    public static bool PairAllowed(User left, User right, DateTimeOffset now, bool allowAdultMinor) =>
        MinorProtectionPolicy.CanBeFriends(left.AgeOn(now), right.AgeOn(now), allowAdultMinor);

    public static async Task<bool> FullBlockBetweenAsync(
        IApplicationDbContext db,
        Guid left,
        Guid right,
        CancellationToken cancellationToken) =>
        await db.UserBlocks.AnyAsync(
            item => item.Scope == UserBlockScope.Full
                && ((item.BlockerId == left && item.BlockedUserId == right)
                    || (item.BlockerId == right && item.BlockedUserId == left)),
            cancellationToken);

    public static bool IsVisibleAccount(User? user) => user is { Status: UserStatus.Active };

    public static async Task NotifyAsync(
        IApplicationDbContext db,
        Guid recipientId,
        NotificationType type,
        string title,
        string body,
        Guid aboutUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var muted = await db.NotificationPreferences.AnyAsync(
            item => item.UserId == recipientId
                && item.Type == type
                && item.Channel == NotificationChannel.InApp
                && !item.IsEnabled,
            cancellationToken);
        if (muted)
        {
            return;
        }

        db.Notifications.Add(Notification.Create(recipientId, type, title, body, ContentTargetType.User, aboutUserId, now));
    }
}

internal static class ProfileCards
{
    public static string DisplayName(User user, UserProfile? profile) =>
        string.IsNullOrWhiteSpace(profile?.DisplayName) ? user.UserName.Value : profile!.DisplayName;

    public static string? Avatar(IPublicLinks links, IMediaUrlSigner urls, User user, UserProfile? profile, DateTimeOffset now)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.AvatarUrl))
        {
            return null;
        }

        if (profile.AvatarKind == AvatarKind.Custom && profile.AvatarUrl.StartsWith("avatars/", StringComparison.Ordinal))
        {
            var signed = urls.Sign(user.Id, now.AddHours(6));
            return links.Absolute($"/api/v1/avatars/{user.Id}/content?exp={signed.ExpiresUnix}&sig={signed.Signature}");
        }

        return profile.AvatarUrl.StartsWith('/') ? links.Absolute(profile.AvatarUrl) : profile.AvatarUrl;
    }
}

internal static class SocialErrors
{
    public static Error NotAvailable() => Error.NotFound("profile.not_available", SocialRules.HiddenProfile);

    public static Error Self() => Error.Validation("friendship.self", "No puedes enviarte una solicitud a ti mismo.");

    public static Error AlreadyFriends() => Error.Conflict("friendship.already_friends", "Ya son amigos.");

    public static Error RateLimited() => Error.Conflict("friendship.rate_limited", "Llegaste al límite de solicitudes. Inténtalo más tarde.");

    public static Error SearchLimited() => Error.Conflict("friendship.search_limited", "Buscaste demasiadas veces. Espera un momento.");

    public static Error NotPending() => Error.Conflict("friendship.not_pending", "Esa solicitud ya no está pendiente.");

    public static Error NotAllowed() => Error.Forbidden("friendship.not_allowed", "No puedes hacer eso con esta solicitud.");

    public static Error NotFriend() => Error.NotFound("friendship.not_found", "No encontramos esa amistad.");

    public static Error HistoryFilter() => Error.Validation("friendship.history_filter", "Elige pendientes, aceptadas o rechazadas.");

    public static Error InvitationInvalid() => Error.NotFound("invitation.not_valid", "Esta invitación ya no sirve.");

    public static Error MinorPublic() => Error.Forbidden("profile.minor_must_be_private", "Un menor de edad tiene que mantener el perfil privado.");

    public static Error ConsentRequired() => Error.Validation("profile.consent_required", "Para hacer público el perfil tienes que aceptar el texto vigente.");
}
