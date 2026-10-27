using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Application.Features.Social;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Features.Location;

internal static class LocationRules
{
    public const int MaxPrivacyZones = 5;
    public const int MaxSelectedFriends = 100;
    public const double MaxPublicSpanDegrees = 3;

    public static async Task<IReadOnlyDictionary<string, string>> SettingsAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await ReportSettings.LoadAsync(db, cancellationToken);

    public static int Int(IReadOnlyDictionary<string, string> settings, string key, int fallback) =>
        ReportSettings.Int(settings, key, fallback);

    public static async Task<bool> PublishesAnonymouslyAsync(IApplicationDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Reports.AnyAsync(
            item => item.AuthorId == userId && item.IsAnonymous && item.Status == ReportStatus.Active && item.ExpiresAt > now,
            cancellationToken)
        || await db.LiveStreams.AnyAsync(
            item => item.StreamerId == userId && item.IsAnonymous && item.Status == LiveStreamStatus.Live,
            cancellationToken);

    public static async Task<bool> GuardianAllowsLocationAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var consent = await db.GuardianConsents
            .Where(item => item.MinorUserId == userId && item.Status == GuardianConsentStatus.Authorized)
            .OrderByDescending(item => item.AuthorizedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return consent?.LocationSharingAuthorized == true;
    }

    public static async Task<IReadOnlyList<Guid>> AcceptedFriendIdsAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.Friendships
            .Where(item => item.Status == FriendshipStatus.Accepted && (item.RequesterId == userId || item.AddresseeId == userId))
            .ToListAsync(cancellationToken);
        return rows.Select(item => item.OtherParty(userId)).Distinct().ToArray();
    }

    public static async Task<IReadOnlyList<Guid>> FriendViewersAsync(
        IApplicationDbContext db,
        User subject,
        UserProfile? profile,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool retracting = false)
    {
        if (profile is null || !profile.ShareLocationWithFriends)
        {
            return [];
        }

        if (!retracting && (!profile.IsFriendSharingActive(now) || profile.IsGhost(now)))
        {
            return [];
        }

        if (!retracting && await PublishesAnonymouslyAsync(db, subject.Id, now, cancellationToken))
        {
            return [];
        }

        var friendIds = await AcceptedFriendIdsAsync(db, subject.Id, cancellationToken);
        if (friendIds.Count == 0)
        {
            return [];
        }

        var users = await db.Users.Where(item => friendIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        Guid[]? selected = null;
        if (profile.FriendLocationSharingMode == LocationSharingMode.SelectedFriends)
        {
            selected = await db.LocationShareRecipients
                .Where(item => item.UserId == subject.Id)
                .Select(item => item.FriendUserId)
                .ToArrayAsync(cancellationToken);
        }

        var viewers = new List<Guid>();
        foreach (var friend in users)
        {
            if (!SocialRules.IsVisibleAccount(friend)
                || !SocialRules.PairAllowed(subject, friend, now, allow)
                || await SocialRules.FullBlockBetweenAsync(db, subject.Id, friend.Id, cancellationToken))
            {
                continue;
            }

            if (selected is not null && !selected.Contains(friend.Id))
            {
                continue;
            }

            viewers.Add(friend.Id);
        }

        return viewers;
    }

    public static (bool Hide, int PrecisionMeters) ZoneEffect(IEnumerable<PrivacyZone> zones, GeoPoint raw, int friendPrecision)
    {
        var containing = zones.Where(zone => zone.Contains(raw)).ToArray();
        if (containing.Any(zone => zone.Mode == PrivacyZoneMode.Hidden))
        {
            return (true, friendPrecision);
        }

        var approximate = containing.Where(zone => zone.Mode == PrivacyZoneMode.Approximate).Select(zone => zone.RadiusMeters).DefaultIfEmpty(0).Max();
        return (false, Math.Max(friendPrecision, approximate));
    }

    public static async Task<IReadOnlyList<Guid>> StopSharingAsync(
        IApplicationDbContext db,
        User subject,
        UserProfile profile,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var viewers = await FriendViewersAsync(db, subject, profile, now, cancellationToken, retracting: true);
        profile.DisableFriendLocationSharing();
        await DeleteLocationAsync(db, subject.Id, cancellationToken);
        return viewers;
    }

    public static async Task DeleteLocationAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.UserLocations.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        db.UserLocations.RemoveRange(rows);
    }

    public static async Task ForgetPairAsync(IFriendLocationPublisher? publisher, Guid left, Guid right, CancellationToken cancellationToken)
    {
        if (publisher is null)
        {
            return;
        }

        await publisher.RemoveAsync(left, right, cancellationToken);
        await publisher.RemoveAsync(right, left, cancellationToken);
    }

    public static async Task RemoveFromAsync(IFriendLocationPublisher? publisher, IEnumerable<Guid> viewers, Guid subjectId, CancellationToken cancellationToken)
    {
        if (publisher is null)
        {
            return;
        }

        foreach (var viewerId in viewers)
        {
            await publisher.RemoveAsync(viewerId, subjectId, cancellationToken);
        }
    }

    public static FriendLocationPush PushFor(User user, UserProfile? profile, UserLocation? location, IPublicLinks links, IMediaUrlSigner urls, DateTimeOffset now) =>
        new(
            user.Id,
            user.UserName.Value,
            ProfileCards.DisplayName(user, profile),
            ProfileCards.Avatar(links, urls, user, profile, now),
            location?.Point.Latitude,
            location?.Point.Longitude,
            location is { IsLive: true } && (location.ExpiresAt is null || location.ExpiresAt > now),
            location?.LastSeenAt);

    public static DateTimeOffset? EndsAt(string? duration, DateTimeOffset now) => duration switch
    {
        "1h" => now.AddHours(1),
        "8h" => now.AddHours(8),
        "until_off" or null or "" => null,
        _ => null,
    };

    public static DateTimeOffset? GhostUntil(string? ghost, DateTimeOffset now) => ghost switch
    {
        "1h" => now.AddHours(1),
        "8h" => now.AddHours(8),
        "tomorrow" => NextColombiaMidnight(now),
        _ => null,
    };

    public static DateTimeOffset NextColombiaMidnight(DateTimeOffset now)
    {
        var midnight = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddHours(5);
        return midnight <= now ? midnight.AddDays(1) : midnight;
    }

    public static bool ReminderDue(User user, UserProfile profile, DateTimeOffset now, int days) 
    {
        if (!user.IsMinor(now) || !profile.IsFriendSharingActive(now))
        {
            return false;
        }

        var mark = profile.FriendLocationRemindedAt ?? profile.FriendLocationConsentedAt;
        return mark is null || mark.Value.AddDays(days) <= now;
    }
}

internal static class LocationErrors
{
    public static Error SharingOff() => Error.Forbidden("location.sharing_off", "La ubicación compartida está apagada.");

    public static Error RateLimited() => Error.Conflict("location.rate_limited", "Enviaste la ubicación demasiadas veces. Espera un momento.");

    public static Error ReadingRejected() => Error.Validation("location.reading_rejected", "Esa lectura de ubicación no se puede usar.");

    public static Error ConsentRequired() => Error.Validation("location.consent_required", "Para compartir ubicación tienes que aceptar el texto vigente.");

    public static Error GuardianRequired() => Error.Forbidden("location.guardian_required", "Para compartir ubicación, tu representante tiene que autorizarlo.");

    public static Error MinorPublic() => Error.Forbidden("location.minor_public", "Un menor de edad no aparece en el mapa general.");

    public static Error ProfilePrivate() => Error.Conflict("location.profile_private", "El mapa general solo muestra perfiles públicos.");

    public static Error ZoneLimit() => Error.Conflict("location.zone_limit", "Puedes guardar hasta 5 zonas de privacidad.");

    public static Error ZoneNotFound() => Error.NotFound("location.zone_not_found", "No encontramos esa zona.");

    public static Error AreaTooLarge() => Error.Validation("location.area_too_large", "Acerca el mapa para ver personas.");
}
