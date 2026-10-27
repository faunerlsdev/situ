using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Social;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;

namespace Situ.Application.Features.Location;

public sealed record PostLocationRequest(Guid UserId, double Latitude, double Longitude, double? AccuracyMeters, DateTimeOffset? CapturedAt);

public sealed record PostLocationResponse(bool Live, bool HiddenBecauseAnonymous, bool HiddenByZone);

public sealed class PostLocationHandler(IApplicationDbContext db, IClock clock, IFriendLocationPublisher publisher, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<PostLocationRequest, Result<PostLocationResponse>>
{
    public async Task<Result<PostLocationResponse>> Handle(PostLocationRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        GeoPoint raw;
        try
        {
            raw = new GeoPoint(request.Latitude, request.Longitude);
        }
        catch (DomainException)
        {
            return Result<PostLocationResponse>.Failure(LocationErrors.ReadingRejected());
        }

        var settings = await LocationRules.SettingsAsync(db, cancellationToken);
        var maxAccuracy = LocationRules.Int(settings, SystemSettingKeys.LocationMaxAccuracyMeters, 200);
        var maxAge = LocationRules.Int(settings, SystemSettingKeys.LocationMaxReadingAgeSeconds, 120);
        if (request.AccuracyMeters is null or < 0 || request.AccuracyMeters > maxAccuracy)
        {
            return Result<PostLocationResponse>.Failure(LocationErrors.ReadingRejected());
        }

        if (request.CapturedAt is { } captured && (captured < now.AddSeconds(-maxAge) || captured > now.AddMinutes(2)))
        {
            return Result<PostLocationResponse>.Failure(LocationErrors.ReadingRejected());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId && item.Status == UserStatus.Active, cancellationToken);
        var profile = user is null ? null : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (user is null || profile is null)
        {
            return Result<PostLocationResponse>.Failure(LocationErrors.SharingOff());
        }

        if (await ExpireSharingAsync(user, profile, now, cancellationToken) is { } expired && expired)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var friendOn = profile.IsFriendSharingActive(now);
        var publicOn = !user.IsMinor(now)
            && profile.Visibility == ProfileVisibility.Public
            && profile.PublicLocationMode != PublicLocationMode.Hidden
            && profile.PublicLocationConsentedAt is not null;
        if (!friendOn && !publicOn)
        {
            return Result<PostLocationResponse>.Failure(LocationErrors.SharingOff());
        }

        var limit = LocationRules.Int(settings, SystemSettingKeys.LocationPostsPerMinute, 4);
        var since = now.AddMinutes(-1);
        var recent = await db.AuditLogs.CountAsync(
            item => item.ActorUserId == user.Id && item.Action == "location.post" && item.OccurredAt >= since,
            cancellationToken);
        if (recent >= limit)
        {
            return Result<PostLocationResponse>.Failure(LocationErrors.RateLimited());
        }

        Audit.Write(db, user.Id, "location.post", "User", user.Id, null, now);
        var anonymous = await LocationRules.PublishesAnonymouslyAsync(db, user.Id, now, cancellationToken);
        var zones = await db.PrivacyZones.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
        var friendPrecision = LocationRules.Int(settings, SystemSettingKeys.LocationFriendPrecisionMeters, 25);
        var publicPrecision = LocationRules.Int(settings, SystemSettingKeys.LocationPublicPrecisionMeters, 300);
        var effect = LocationRules.ZoneEffect(zones, raw, friendOn ? friendPrecision : publicPrecision);
        if (profile.IsGhost(now) || anonymous || effect.Hide)
        {
            var retract = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken, retracting: true);
            await LocationRules.DeleteLocationAsync(db, user.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await LocationRules.RemoveFromAsync(publisher, retract, user.Id, cancellationToken);
            return Result<PostLocationResponse>.Success(new PostLocationResponse(false, anonymous, effect.Hide));
        }

        var storedPrecision = friendOn ? effect.PrecisionMeters : Math.Max(effect.PrecisionMeters, publicPrecision);
        var rounded = LocationGrid.Round(raw, storedPrecision);
        var ttl = TimeSpan.FromMinutes(LocationRules.Int(settings, SystemSettingKeys.LocationTtlMinutes, 15));
        var row = await db.UserLocations.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (row is null)
        {
            row = UserLocation.Create(user.Id, rounded, request.AccuracyMeters, null, now);
            db.UserLocations.Add(row);
        }

        row.Publish(rounded, request.AccuracyMeters, null, now.Add(ttl), now);
        await db.SaveChangesAsync(cancellationToken);
        if (friendOn && !profile.IsGhost(now))
        {
            var viewers = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken);
            var push = LocationRules.PushFor(user, profile, row, links, urls, now);
            foreach (var viewerId in viewers)
            {
                await publisher.UpdateAsync(viewerId, push, cancellationToken);
            }
        }

        return Result<PostLocationResponse>.Success(new PostLocationResponse(true, false, false));
    }

    private async Task<bool> ExpireSharingAsync(User user, UserProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!profile.ShareLocationWithFriends || profile.FriendLocationSharingEndsAt is not { } ends || ends > now)
        {
            return false;
        }

        var viewers = await LocationRules.StopSharingAsync(db, user, profile, now, cancellationToken);
        await LocationRules.RemoveFromAsync(publisher, viewers, user.Id, cancellationToken);
        return true;
    }

    internal static bool IsPublicLocationOn(User user, UserProfile profile, DateTimeOffset now) =>
        !user.IsMinor(now)
        && !profile.IsGhost(now)
        && profile.Visibility == ProfileVisibility.Public
        && profile.PublicLocationMode != PublicLocationMode.Hidden
        && profile.PublicLocationConsentedAt is not null;
}

public sealed record PauseLocationRequest(Guid UserId);

public sealed record PauseLocationResponse(bool Live, DateTimeOffset? LastSeenAt);

public sealed class PauseLocationHandler(IApplicationDbContext db, IClock clock, IFriendLocationPublisher publisher, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<PauseLocationRequest, Result<PauseLocationResponse>>
{
    public async Task<Result<PauseLocationResponse>> Handle(PauseLocationRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var settings = await LocationRules.SettingsAsync(db, cancellationToken);
        var row = await db.UserLocations.FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (row is null)
        {
            return Result<PauseLocationResponse>.Success(new PauseLocationResponse(false, null));
        }

        var round = LocationRules.Int(settings, SystemSettingKeys.LocationLastSeenRoundMinutes, 5);
        var ttl = TimeSpan.FromMinutes(LocationRules.Int(settings, SystemSettingKeys.LocationTtlMinutes, 15));
        row.MarkLastSeen(LocationGrid.RoundTime(now, round), now.Add(ttl));
        await db.SaveChangesAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var profile = user is null ? null : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (user is not null && profile is not null)
        {
            var viewers = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken);
            var push = LocationRules.PushFor(user, profile, row, links, urls, now);
            foreach (var viewerId in viewers)
            {
                await publisher.UpdateAsync(viewerId, push, cancellationToken);
            }
        }

        return Result<PauseLocationResponse>.Success(new PauseLocationResponse(false, row.LastSeenAt));
    }
}

public sealed record GetLocationSharingRequest(Guid UserId);

public sealed record LocationSharingView(
    bool Enabled,
    string Mode,
    DateTimeOffset? EndsAt,
    IReadOnlyList<Guid> FriendIds,
    bool ConsentGranted,
    string RequiredConsentVersion,
    string ConsentText,
    bool Ghost,
    DateTimeOffset? GhostUntil,
    bool IsMinor,
    bool GuardianAllows,
    bool ReminderDue,
    bool HiddenBecauseAnonymous,
    string PublicMode,
    bool PublicConsentGranted,
    string PublicConsentVersion,
    string PublicConsentText,
    bool ProfilePublic,
    int EmitIntervalSeconds);

public sealed class GetLocationSharingHandler(IApplicationDbContext db, IClock clock, IFriendLocationPublisher publisher)
    : IHandler<GetLocationSharingRequest, Result<LocationSharingView>>
{
    public async Task<Result<LocationSharingView>> Handle(GetLocationSharingRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.SharingOff());
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile is null)
        {
            var empty = await LocationRules.SettingsAsync(db, cancellationToken);
            var emptyVersion = empty.GetValueOrDefault(SystemSettingKeys.LocationFriendConsentVersion) ?? "2026-10-27";
            var emptyPublic = empty.GetValueOrDefault(SystemSettingKeys.LocationPublicConsentVersion) ?? "2026-10-27";
            return Result<LocationSharingView>.Success(new LocationSharingView(
                false,
                nameof(LocationSharingMode.AllFriends),
                null,
                [],
                false,
                emptyVersion,
                empty.GetValueOrDefault(SystemSettingKeys.LocationFriendConsentText) ?? "",
                false,
                null,
                user.IsMinor(now),
                !user.IsMinor(now) || await LocationRules.GuardianAllowsLocationAsync(db, user.Id, cancellationToken),
                false,
                await LocationRules.PublishesAnonymouslyAsync(db, user.Id, now, cancellationToken),
                nameof(PublicLocationMode.Hidden),
                false,
                emptyPublic,
                empty.GetValueOrDefault(SystemSettingKeys.LocationPublicConsentText) ?? "",
                false,
                LocationRules.Int(empty, SystemSettingKeys.LocationEmitIntervalSeconds, 20)));
        }

        if (profile.ShareLocationWithFriends && profile.FriendLocationSharingEndsAt is { } ends && ends <= now)
        {
            var viewers = await LocationRules.StopSharingAsync(db, user, profile, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await LocationRules.RemoveFromAsync(publisher, viewers, user.Id, cancellationToken);
        }

        return Result<LocationSharingView>.Success(await DescribeAsync(db, user, profile, now, cancellationToken));
    }

    internal static async Task<UserProfile> LoadOrCreateAsync(IApplicationDbContext db, User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = UserProfile.CreateDefault(user.Id, user.UserName.Value);
        profile.MarkCreated(now);
        db.UserProfiles.Add(profile);
        return profile;
    }

    internal static async Task<LocationSharingView> DescribeAsync(IApplicationDbContext db, User user, UserProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = await LocationRules.SettingsAsync(db, cancellationToken);
        var friendIds = await db.LocationShareRecipients.Where(item => item.UserId == user.Id).Select(item => item.FriendUserId).ToListAsync(cancellationToken);
        var days = LocationRules.Int(settings, SystemSettingKeys.LocationMinorReminderDays, 7);
        var version = settings.GetValueOrDefault(SystemSettingKeys.LocationFriendConsentVersion) ?? "2026-10-27";
        var publicVersion = settings.GetValueOrDefault(SystemSettingKeys.LocationPublicConsentVersion) ?? "2026-10-27";
        return new LocationSharingView(
            profile.IsFriendSharingActive(now),
            profile.FriendLocationSharingMode.ToString(),
            profile.FriendLocationSharingEndsAt,
            friendIds,
            profile.FriendLocationConsentVersion == version && profile.FriendLocationConsentedAt is not null,
            version,
            settings.GetValueOrDefault(SystemSettingKeys.LocationFriendConsentText) ?? "",
            profile.IsGhost(now),
            profile.IsGhost(now) ? profile.GhostModeUntil : null,
            user.IsMinor(now),
            !user.IsMinor(now) || await LocationRules.GuardianAllowsLocationAsync(db, user.Id, cancellationToken),
            LocationRules.ReminderDue(user, profile, now, days),
            await LocationRules.PublishesAnonymouslyAsync(db, user.Id, now, cancellationToken),
            profile.PublicLocationMode.ToString(),
            profile.PublicLocationConsentVersion == publicVersion && profile.PublicLocationConsentedAt is not null,
            publicVersion,
            settings.GetValueOrDefault(SystemSettingKeys.LocationPublicConsentText) ?? "",
            profile.Visibility == ProfileVisibility.Public && !user.IsMinor(now),
            LocationRules.Int(settings, SystemSettingKeys.LocationEmitIntervalSeconds, 20));
    }
}

public sealed record SetLocationSharingRequest(
    Guid UserId,
    bool Enabled,
    string? Mode,
    string? Duration,
    IReadOnlyList<Guid>? FriendIds,
    string? ConsentVersion,
    string? Ghost);

public sealed class SetLocationSharingHandler(IApplicationDbContext db, IClock clock, IFriendLocationPublisher publisher)
    : IHandler<SetLocationSharingRequest, Result<LocationSharingView>>
{
    public async Task<Result<LocationSharingView>> Handle(SetLocationSharingRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId && item.Status == UserStatus.Active, cancellationToken);
        if (user is null)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.SharingOff());
        }

        if (request.Duration is not null && request.Duration is not ("1h" or "8h" or "until_off"))
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ReadingRejected());
        }

        if (request.Mode is not null && request.Mode is not ("AllFriends" or "SelectedFriends"))
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ReadingRejected());
        }

        if (request.Ghost is not null && request.Ghost is not ("off" or "1h" or "8h" or "tomorrow"))
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ReadingRejected());
        }

        var settings = await LocationRules.SettingsAsync(db, cancellationToken);
        var required = settings.GetValueOrDefault(SystemSettingKeys.LocationFriendConsentVersion) ?? "2026-10-27";
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (request.Enabled && user.IsMinor(now) && !await LocationRules.GuardianAllowsLocationAsync(db, user.Id, cancellationToken))
        {
            return Result<LocationSharingView>.Failure(LocationErrors.GuardianRequired());
        }

        var alreadyConsented = profile?.FriendLocationConsentVersion == required && profile.FriendLocationConsentedAt is not null;
        if (request.Enabled && !alreadyConsented && request.ConsentVersion != required)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ConsentRequired());
        }

        profile ??= await GetLocationSharingHandler.LoadOrCreateAsync(db, user, now, cancellationToken);
        var previous = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken, retracting: true);
        if (request.Enabled)
        {
            if (!alreadyConsented)
            {
                profile.GrantFriendLocationConsent(now, required);
            }

            var mode = request.Mode == "SelectedFriends" ? LocationSharingMode.SelectedFriends : request.Mode == "AllFriends" ? LocationSharingMode.AllFriends : profile.FriendLocationSharingMode;
            var ends = request.Duration is null && profile.IsFriendSharingActive(now)
                ? profile.FriendLocationSharingEndsAt
                : LocationRules.EndsAt(request.Duration, now);
            try
            {
                profile.EnableFriendLocationSharing(mode, ends, now);
            }
            catch (DomainException)
            {
                return Result<LocationSharingView>.Failure(LocationErrors.ReadingRejected());
            }
        }
        else if (profile.ShareLocationWithFriends)
        {
            profile.DisableFriendLocationSharing();
            await LocationRules.DeleteLocationAsync(db, user.Id, cancellationToken);
        }

        if (request.FriendIds is not null)
        {
            var accepted = await LocationRules.AcceptedFriendIdsAsync(db, user.Id, cancellationToken);
            var chosen = request.FriendIds.Where(id => id != user.Id && accepted.Contains(id)).Distinct().Take(LocationRules.MaxSelectedFriends).ToArray();
            var existing = await db.LocationShareRecipients.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
            db.LocationShareRecipients.RemoveRange(existing);
            foreach (var friendId in chosen)
            {
                var row = LocationShareRecipient.Create(user.Id, friendId);
                row.MarkCreated(now);
                db.LocationShareRecipients.Add(row);
            }
        }

        if (request.Ghost == "off")
        {
            profile.ClearGhostMode();
        }
        else if (request.Ghost is not null)
        {
            profile.ActivateGhostMode(LocationRules.GhostUntil(request.Ghost, now)!.Value, now);
            await LocationRules.DeleteLocationAsync(db, user.Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        var current = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken);
        var dropped = previous.Where(id => !current.Contains(id)).ToArray();
        await LocationRules.RemoveFromAsync(publisher, dropped, user.Id, cancellationToken);
        return Result<LocationSharingView>.Success(await GetLocationSharingHandler.DescribeAsync(db, user, profile, now, cancellationToken));
    }
}

public sealed record AcknowledgeLocationReminderRequest(Guid UserId);

public sealed class AcknowledgeLocationReminderHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AcknowledgeLocationReminderRequest, Result<LocationSharingView>>
{
    public async Task<Result<LocationSharingView>> Handle(AcknowledgeLocationReminderRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var profile = user is null ? null : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (user is null || profile is null)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.SharingOff());
        }

        profile.RememberFriendLocationReminder(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<LocationSharingView>.Success(await GetLocationSharingHandler.DescribeAsync(db, user, profile, now, cancellationToken));
    }
}

public sealed record SetPublicLocationRequest(Guid UserId, string Mode, string? ConsentVersion);

public sealed class SetPublicLocationHandler(IApplicationDbContext db, IClock clock, IFriendLocationPublisher publisher)
    : IHandler<SetPublicLocationRequest, Result<LocationSharingView>>
{
    public async Task<Result<LocationSharingView>> Handle(SetPublicLocationRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId && item.Status == UserStatus.Active, cancellationToken);
        if (user is null || !Enum.TryParse(request.Mode, ignoreCase: true, out PublicLocationMode mode))
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ReadingRejected());
        }

        if (user.IsMinor(now) && mode != PublicLocationMode.Hidden)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.MinorPublic());
        }

        var settings = await LocationRules.SettingsAsync(db, cancellationToken);
        var required = settings.GetValueOrDefault(SystemSettingKeys.LocationPublicConsentVersion) ?? "2026-10-27";
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (mode != PublicLocationMode.Hidden && profile?.Visibility != ProfileVisibility.Public)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ProfilePrivate());
        }

        var consented = profile?.PublicLocationConsentVersion == required && profile.PublicLocationConsentedAt is not null;
        if (mode != PublicLocationMode.Hidden && !consented && request.ConsentVersion != required)
        {
            return Result<LocationSharingView>.Failure(LocationErrors.ConsentRequired());
        }

        profile ??= await GetLocationSharingHandler.LoadOrCreateAsync(db, user, now, cancellationToken);
        try
        {
            profile.SetPublicLocation(user.AgeOn(now), mode, mode == PublicLocationMode.Hidden ? profile.PublicLocationConsentedAt : now, mode == PublicLocationMode.Hidden ? profile.PublicLocationConsentVersion : required);
        }
        catch (DomainException exception) when (exception.Code == "location.minor_public")
        {
            return Result<LocationSharingView>.Failure(LocationErrors.MinorPublic());
        }

        if (mode == PublicLocationMode.Hidden)
        {
            var viewers = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken);
            if (!profile.IsFriendSharingActive(now))
            {
                await LocationRules.DeleteLocationAsync(db, user.Id, cancellationToken);
                await LocationRules.RemoveFromAsync(publisher, viewers, user.Id, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<LocationSharingView>.Success(await GetLocationSharingHandler.DescribeAsync(db, user, profile, now, cancellationToken));
    }
}

public sealed record PrivacyZoneView(Guid Id, string Name, double Latitude, double Longitude, int RadiusMeters, string Mode);

public sealed record ListPrivacyZonesRequest(Guid UserId);

public sealed record ListPrivacyZonesResponse(IReadOnlyList<PrivacyZoneView> Zones);

public sealed class ListPrivacyZonesHandler(IApplicationDbContext db)
    : IHandler<ListPrivacyZonesRequest, Result<ListPrivacyZonesResponse>>
{
    public async Task<Result<ListPrivacyZonesResponse>> Handle(ListPrivacyZonesRequest request, CancellationToken cancellationToken)
    {
        var zones = await db.PrivacyZones.Where(item => item.UserId == request.UserId).ToListAsync(cancellationToken);
        return Result<ListPrivacyZonesResponse>.Success(new ListPrivacyZonesResponse(zones.Select(Map).ToArray()));
    }

    internal static PrivacyZoneView Map(PrivacyZone zone) =>
        new(zone.Id, zone.Name, zone.Center.Latitude, zone.Center.Longitude, zone.RadiusMeters, zone.Mode.ToString());
}

public sealed record SavePrivacyZoneRequest(Guid UserId, Guid? ZoneId, string Name, double Latitude, double Longitude, int RadiusMeters, string Mode);

public sealed class SavePrivacyZoneHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SavePrivacyZoneRequest, Result<PrivacyZoneView>>
{
    public async Task<Result<PrivacyZoneView>> Handle(SavePrivacyZoneRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Mode, ignoreCase: true, out PrivacyZoneMode mode))
        {
            return Result<PrivacyZoneView>.Failure(LocationErrors.ReadingRejected());
        }

        GeoPoint center;
        try
        {
            center = new GeoPoint(request.Latitude, request.Longitude);
        }
        catch (DomainException)
        {
            return Result<PrivacyZoneView>.Failure(LocationErrors.ReadingRejected());
        }

        var now = clock.UtcNow;
        PrivacyZone? zone = null;
        if (request.ZoneId is { } zoneId)
        {
            zone = await db.PrivacyZones.FirstOrDefaultAsync(item => item.Id == zoneId && item.UserId == request.UserId, cancellationToken);
            if (zone is null)
            {
                return Result<PrivacyZoneView>.Failure(LocationErrors.ZoneNotFound());
            }
        }
        else
        {
            var count = await db.PrivacyZones.CountAsync(item => item.UserId == request.UserId, cancellationToken);
            if (count >= LocationRules.MaxPrivacyZones)
            {
                return Result<PrivacyZoneView>.Failure(LocationErrors.ZoneLimit());
            }
        }

        try
        {
            if (zone is null)
            {
                zone = PrivacyZone.Create(request.UserId, request.Name.Trim(), center, request.RadiusMeters, mode);
                zone.MarkCreated(now);
                db.PrivacyZones.Add(zone);
            }
            else
            {
                zone.Update(request.Name.Trim(), center, request.RadiusMeters, mode);
                zone.MarkUpdated(now);
            }
        }
        catch (DomainException)
        {
            return Result<PrivacyZoneView>.Failure(LocationErrors.ReadingRejected());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<PrivacyZoneView>.Success(ListPrivacyZonesHandler.Map(zone));
    }
}

public sealed record DeletePrivacyZoneRequest(Guid UserId, Guid ZoneId);

public sealed class DeletePrivacyZoneHandler(IApplicationDbContext db)
    : IHandler<DeletePrivacyZoneRequest, Result>
{
    public async Task<Result> Handle(DeletePrivacyZoneRequest request, CancellationToken cancellationToken)
    {
        var zone = await db.PrivacyZones.FirstOrDefaultAsync(item => item.Id == request.ZoneId && item.UserId == request.UserId, cancellationToken);
        if (zone is null)
        {
            return Result.Failure(LocationErrors.ZoneNotFound());
        }

        db.PrivacyZones.Remove(zone);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetMapPeopleRequest(Guid UserId, double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude);

public sealed record MapPersonView(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    double Latitude,
    double Longitude,
    bool Live,
    DateTimeOffset? LastSeenAt);

public sealed record GetMapPeopleResponse(IReadOnlyList<MapPersonView> People);

public sealed class GetMapPeopleHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<GetMapPeopleRequest, Result<GetMapPeopleResponse>>
{
    public async Task<Result<GetMapPeopleResponse>> Handle(GetMapPeopleRequest request, CancellationToken cancellationToken)
    {
        if (request.MaxLatitude - request.MinLatitude > LocationRules.MaxPublicSpanDegrees
            || request.MaxLongitude - request.MinLongitude > LocationRules.MaxPublicSpanDegrees)
        {
            return Result<GetMapPeopleResponse>.Failure(LocationErrors.AreaTooLarge());
        }

        var now = clock.UtcNow;
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (viewer is null)
        {
            return Result<GetMapPeopleResponse>.Success(new GetMapPeopleResponse([]));
        }

        var settings = await LocationRules.SettingsAsync(db, cancellationToken);
        var publicPrecision = LocationRules.Int(settings, SystemSettingKeys.LocationPublicPrecisionMeters, 300);
        var rows = await db.UserLocations.Where(item => item.ExpiresAt == null || item.ExpiresAt > now).ToListAsync(cancellationToken);
        var inside = rows.Where(item =>
            item.UserId != viewer.Id
            && item.Point.Latitude >= request.MinLatitude
            && item.Point.Latitude <= request.MaxLatitude
            && item.Point.Longitude >= request.MinLongitude
            && item.Point.Longitude <= request.MaxLongitude).ToArray();
        if (inside.Length == 0)
        {
            return Result<GetMapPeopleResponse>.Success(new GetMapPeopleResponse([]));
        }

        var ids = inside.Select(item => item.UserId).ToArray();
        var users = await db.Users.Where(item => ids.Contains(item.Id) && item.Status == UserStatus.Active).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => ids.Contains(item.UserId)).ToListAsync(cancellationToken);
        var zones = await db.PrivacyZones.Where(item => ids.Contains(item.UserId)).ToListAsync(cancellationToken);
        var people = new List<MapPersonView>();
        foreach (var row in inside)
        {
            var user = users.FirstOrDefault(item => item.Id == row.UserId);
            var profile = profiles.FirstOrDefault(item => item.UserId == row.UserId);
            if (user is null || profile is null || !PostLocationHandler.IsPublicLocationOn(user, profile, now))
            {
                continue;
            }

            if (await LocationRules.PublishesAnonymouslyAsync(db, user.Id, now, cancellationToken)
                || await SocialRulesBlocked(db, viewer.Id, user.Id, cancellationToken))
            {
                continue;
            }

            var coarse = Math.Max(publicPrecision, zones.Where(zone => zone.UserId == user.Id && zone.Mode == PrivacyZoneMode.Approximate && zone.Contains(row.Point)).Select(zone => zone.RadiusMeters).DefaultIfEmpty(0).Max());
            var point = LocationGrid.Round(row.Point, coarse);
            people.Add(new MapPersonView(
                user.Id,
                user.UserName.Value,
                ProfileCards.DisplayName(user, profile),
                ProfileCards.Avatar(links, urls, user, profile, now),
                point.Latitude,
                point.Longitude,
                row.IsLive,
                row.LastSeenAt));
        }

        return Result<GetMapPeopleResponse>.Success(new GetMapPeopleResponse(people));
    }

    private static async Task<bool> SocialRulesBlocked(IApplicationDbContext db, Guid viewerId, Guid subjectId, CancellationToken cancellationToken) =>
        await Features.Social.SocialRules.FullBlockBetweenAsync(db, viewerId, subjectId, cancellationToken);
}

public sealed record ListFriendLocationsRequest(Guid UserId);

public sealed class ListFriendLocationsHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<ListFriendLocationsRequest, Result<GetMapPeopleResponse>>
{
    public async Task<Result<GetMapPeopleResponse>> Handle(ListFriendLocationsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId && item.Status == UserStatus.Active, cancellationToken);
        if (me is null)
        {
            return Result<GetMapPeopleResponse>.Success(new GetMapPeopleResponse([]));
        }

        var friendIds = await LocationRules.AcceptedFriendIdsAsync(db, me.Id, cancellationToken);
        var rows = await db.UserLocations.Where(item => friendIds.Contains(item.UserId) && (item.ExpiresAt == null || item.ExpiresAt > now)).ToListAsync(cancellationToken);
        var users = await db.Users.Where(item => friendIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => friendIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var people = new List<MapPersonView>();
        foreach (var row in rows)
        {
            var user = users.FirstOrDefault(item => item.Id == row.UserId);
            var profile = profiles.FirstOrDefault(item => item.UserId == row.UserId);
            if (user is null || profile is null)
            {
                continue;
            }

            var viewers = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken);
            if (!viewers.Contains(me.Id))
            {
                continue;
            }

            var push = LocationRules.PushFor(user, profile, row, links, urls, now);
            if (push.Latitude is null || push.Longitude is null)
            {
                continue;
            }

            people.Add(new MapPersonView(push.UserId, push.UserName, push.DisplayName, push.AvatarUrl, push.Latitude.Value, push.Longitude.Value, push.Live, push.LastSeenAt));
        }

        return Result<GetMapPeopleResponse>.Success(new GetMapPeopleResponse(people));
    }
}

public sealed record PurgeUserLocationsRequest;

public sealed record PurgeUserLocationsResponse(int Removed);

public sealed class PurgeUserLocationsHandler(IApplicationDbContext db, IClock clock, IFriendLocationPublisher publisher)
    : IHandler<PurgeUserLocationsRequest, Result<PurgeUserLocationsResponse>>
{
    public async Task<Result<PurgeUserLocationsResponse>> Handle(PurgeUserLocationsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expiredSharing = await db.UserProfiles
            .Where(item => item.ShareLocationWithFriends && item.FriendLocationSharingEndsAt != null && item.FriendLocationSharingEndsAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var profile in expiredSharing)
        {
            var user = await db.Users.FirstOrDefaultAsync(item => item.Id == profile.UserId, cancellationToken);
            if (user is null)
            {
                profile.DisableFriendLocationSharing();
                continue;
            }

            var viewers = await LocationRules.StopSharingAsync(db, user, profile, now, cancellationToken);
            await LocationRules.RemoveFromAsync(publisher, viewers, user.Id, cancellationToken);
        }

        var stale = await db.UserLocations.Where(item => item.ExpiresAt != null && item.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var row in stale)
        {
            var user = await db.Users.FirstOrDefaultAsync(item => item.Id == row.UserId, cancellationToken);
            var profile = user is null ? null : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            if (user is not null)
            {
                var viewers = await LocationRules.FriendViewersAsync(db, user, profile, now, cancellationToken, retracting: true);
                await LocationRules.RemoveFromAsync(publisher, viewers, user.Id, cancellationToken);
            }
        }

        db.UserLocations.RemoveRange(stale);
        if (expiredSharing.Count > 0 || stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<PurgeUserLocationsResponse>.Success(new PurgeUserLocationsResponse(stale.Count));
    }
}
