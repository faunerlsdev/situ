using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Interaction.Complaints;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Live;
using Situ.Domain.Moderation;

namespace Situ.Application.Features.Live;

public sealed record CreateLiveRequest(
    Guid UserId,
    string Visibility,
    string Title,
    Guid? CategoryId,
    double? Latitude,
    double? Longitude);

public sealed record CreateLiveResponse(string Id, string PublishUrl, string PublishKey, DateTimeOffset PublishKeyExpiresAt);

public sealed class CreateLiveValidator : AbstractValidator<CreateLiveRequest>
{
    public CreateLiveValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Visibility).Must(value => value is "Public" or "Friends" or "FriendsOnly");
        RuleFor(request => request.Latitude).InclusiveBetween(-90, 90).When(request => request.Latitude is not null);
        RuleFor(request => request.Longitude).InclusiveBetween(-180, 180).When(request => request.Longitude is not null);
    }
}

public sealed class CreateLiveHandler(
    IApplicationDbContext db,
    IClock clock,
    IOptions<LiveStreamOptions> options)
    : IHandler<CreateLiveRequest, Result<CreateLiveResponse>>
{
    public async Task<Result<CreateLiveResponse>> Handle(CreateLiveRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.IngestSecret) || string.IsNullOrWhiteSpace(options.Value.HlsSecret))
        {
            return Result<CreateLiveResponse>.Failure(LiveRules.NotConfigured());
        }

        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<CreateLiveResponse>.Failure(InteractionErrors.CannotPublish());
        }

        var blocked = InteractionAccess.PublishBlock(user);
        if (blocked is not null)
        {
            return Result<CreateLiveResponse>.Failure(blocked);
        }

        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        var visibility = request.Visibility is "Public" ? ContentVisibility.Public : ContentVisibility.FriendsOnly;
        if (request.CategoryId is { } categoryId
            && !await db.ReportCategories.AnyAsync(item => item.Id == categoryId && item.IsActive, cancellationToken))
        {
            return Result<CreateLiveResponse>.Failure(Error.Validation("live.category_invalid", "Esa categoría no está disponible."));
        }

        var active = await db.LiveStreams.CountAsync(
            item => item.Status == LiveStreamStatus.Waiting || item.Status == LiveStreamStatus.Live,
            cancellationToken);
        var maxConcurrent = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.LiveMaxConcurrent, 2), 1, 20);
        if (active >= maxConcurrent)
        {
            return Result<CreateLiveResponse>.Failure(Error.Conflict("live.limit_concurrent", "Ahora mismo no hay cupo para otra transmisión."));
        }

        if (await db.LiveStreams.AnyAsync(
            item => item.StreamerId == user.Id && (item.Status == LiveStreamStatus.Waiting || item.Status == LiveStreamStatus.Live),
            cancellationToken))
        {
            return Result<CreateLiveResponse>.Failure(Error.Conflict("live.already_live", "Ya tienes una transmisión activa."));
        }

        GeoPoint? location = null;
        if (request.Latitude is { } latitude && request.Longitude is { } longitude)
        {
            var meters = visibility == ContentVisibility.FriendsOnly
                ? ReportSettings.Int(settings, SystemSettingKeys.LocationFriendPrecisionMeters, 25)
                : ReportSettings.Int(settings, SystemSettingKeys.LocationPublicPrecisionMeters, 300);
            location = LiveLocation.Round(new GeoPoint(latitude, longitude), meters);
        }

        var path = LiveTokens.NewPath();
        var key = LiveTokens.NewPublishKey();
        var expires = now.AddSeconds(LiveStreamOptions.PublishKeySeconds);
        LiveStream stream;
        try
        {
            stream = LiveStream.Schedule(
                user.Id,
                user.AgeOn(now),
                LiveRules.Flag(settings, SystemSettingKeys.LiveMinorsCanStream),
                request.CategoryId,
                user.CityId,
                location,
                request.Title.Trim(),
                visibility,
                path,
                LiveTokens.HashKey(key),
                expires,
                now);
        }
        catch (DomainException exception)
        {
            return Result<CreateLiveResponse>.Failure(LiveRules.FromDomain(exception));
        }

        db.LiveStreams.Add(stream);
        await db.SaveChangesAsync(cancellationToken);
        return Result<CreateLiveResponse>.Success(new CreateLiveResponse(
            path,
            LiveTokens.PublishUrl(options.Value, path, key),
            key,
            expires));
    }
}

public sealed record EndLiveRequest(Guid UserId, string PublicId, bool Staff);

public sealed class EndLiveHandler(
    IApplicationDbContext db,
    IClock clock,
    IMediaMtxControl media)
    : IHandler<EndLiveRequest, Result>
{
    public async Task<Result> Handle(EndLiveRequest request, CancellationToken cancellationToken)
    {
        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.PublicId, cancellationToken);
        if (stream is null || !stream.IsOpen)
        {
            return Result.Failure(LiveRules.NotFound());
        }

        if (!request.Staff && stream.StreamerId != request.UserId)
        {
            return Result.Failure(Error.Forbidden("live.forbidden", "Solo quien transmite puede terminarla."));
        }

        if (request.Staff && !await StaffGate.HasAsync(db, request.UserId, StaffPermissionCodes.LiveCut, cancellationToken))
        {
            return Result.Failure(InteractionErrors.StaffForbidden());
        }

        var now = clock.UtcNow;
        if (stream.PublicPath is { } path)
        {
            await media.KickPathAsync(path, cancellationToken);
        }

        stream.End(stream.RecordingPath, now);
        if (request.Staff)
        {
            Audit.Write(db, request.UserId, "live.end", "LiveStream", stream.Id, null, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record LiveCard(
    string Id,
    string Title,
    Guid? CategoryId,
    string Visibility,
    string DisplayName,
    double? Latitude,
    double? Longitude,
    DateTimeOffset StartedAt,
    int Viewers,
    string Status);

public sealed record ListLiveRequest(Guid UserId);

public sealed record ListLiveResponse(IReadOnlyList<LiveCard> Streams);

public sealed class ListLiveHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListLiveRequest, Result<ListLiveResponse>>
{
    public async Task<Result<ListLiveResponse>> Handle(ListLiveRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (viewer is null || viewer.Status is Domain.Identity.UserStatus.Deleted or Domain.Identity.UserStatus.Banned or Domain.Identity.UserStatus.Suspended or Domain.Identity.UserStatus.DeletionRequested)
        {
            return Result<ListLiveResponse>.Failure(Error.Forbidden("live.forbidden", "No puedes ver transmisiones."));
        }

        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        var streams = await db.LiveStreams.Where(item => item.Status == LiveStreamStatus.Live && !item.HiddenByStaff).ToListAsync(cancellationToken);
        var cards = new List<LiveCard>();
        foreach (var stream in streams)
        {
            if (await VisibleAsync(db, viewer, stream, settings, now, cancellationToken))
            {
                cards.Add(await CardAsync(db, stream, settings, now, cancellationToken));
            }
        }

        return Result<ListLiveResponse>.Success(new ListLiveResponse(cards));
    }

    internal static async Task<bool> VisibleAsync(
        IApplicationDbContext db,
        Domain.Identity.User viewer,
        LiveStream stream,
        IReadOnlyDictionary<string, string> settings,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool liveOnly = true)
    {
        var streamer = await db.Users.FirstOrDefaultAsync(item => item.Id == stream.StreamerId, cancellationToken);
        if (streamer is null || (liveOnly && stream.Status != LiveStreamStatus.Live))
        {
            return false;
        }

        var friends = viewer.Id == streamer.Id || await LiveRules.FriendsAsync(db, viewer.Id, streamer.Id, cancellationToken);
        var blocked = viewer.Id != streamer.Id && await LiveRules.BlockedAsync(db, viewer.Id, streamer.Id, cancellationToken);
        return LiveAudience.CanWatch(
            viewer,
            streamer,
            stream,
            friends,
            blocked,
            LiveRules.Flag(settings, SystemSettingKeys.LiveMinorsCanStream),
            LiveRules.Flag(settings, SystemSettingKeys.FriendsAllowAdultMinor),
            now);
    }

    internal static async Task<LiveCard> CardAsync(
        IApplicationDbContext db,
        LiveStream stream,
        IReadOnlyDictionary<string, string> settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var viewers = await db.LiveViewerSessions.CountAsync(
            item => item.LiveStreamId == stream.Id && item.ExpiresAt > now,
            cancellationToken);
        return new LiveCard(
            stream.PublicPath ?? string.Empty,
            stream.Title,
            stream.CategoryId,
            stream.Visibility == ContentVisibility.Public ? "Public" : "Friends",
            ReportSettings.DisplayName(settings),
            stream.Location?.Latitude,
            stream.Location?.Longitude,
            stream.StartedAt,
            viewers,
            stream.Status.ToString());
    }
}

public sealed record GetLiveRequest(Guid UserId, string PublicId);

public sealed class GetLiveHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetLiveRequest, Result<LiveCard>>
{
    public async Task<Result<LiveCard>> Handle(GetLiveRequest request, CancellationToken cancellationToken)
    {
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.PublicId, cancellationToken);
        if (viewer is null || stream is null)
        {
            return Result<LiveCard>.Failure(LiveRules.NotFound());
        }

        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        if (!await ListLiveHandler.VisibleAsync(db, viewer, stream, settings, clock.UtcNow, cancellationToken, liveOnly: false))
        {
            return Result<LiveCard>.Failure(LiveRules.NotFound());
        }

        return Result<LiveCard>.Success(await ListLiveHandler.CardAsync(db, stream, settings, clock.UtcNow, cancellationToken));
    }
}

public sealed record WatchLiveRequest(Guid UserId, string PublicId);

public sealed record WatchLiveResponse(string Url, DateTimeOffset ExpiresAt);

public sealed class WatchLiveHandler(
    IApplicationDbContext db,
    IClock clock,
    IOptions<LiveStreamOptions> options)
    : IHandler<WatchLiveRequest, Result<WatchLiveResponse>>
{
    public async Task<Result<WatchLiveResponse>> Handle(WatchLiveRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.HlsSecret))
        {
            return Result<WatchLiveResponse>.Failure(LiveRules.NotConfigured());
        }

        var now = clock.UtcNow;
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.PublicId, cancellationToken);
        if (viewer is null || stream is null || stream.PublicPath is null || stream.Status != LiveStreamStatus.Live)
        {
            return Result<WatchLiveResponse>.Failure(LiveRules.NotFound());
        }

        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        if (!await ListLiveHandler.VisibleAsync(db, viewer, stream, settings, now, cancellationToken))
        {
            return Result<WatchLiveResponse>.Failure(LiveRules.NotFound());
        }

        var maxViewers = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.LiveMaxViewers, 50), 1, 500);
        var watching = await db.LiveViewerSessions.CountAsync(item => item.LiveStreamId == stream.Id && item.ExpiresAt > now, cancellationToken);
        if (viewer.Id != stream.StreamerId && watching >= maxViewers)
        {
            return Result<WatchLiveResponse>.Failure(Error.Conflict("live.viewers_full", "Esta transmisión ya no admite más personas."));
        }

        if (viewer.Id != stream.StreamerId)
        {
            db.LiveViewerSessions.Add(LiveViewerSession.Open(stream.Id, now, TimeSpan.FromSeconds(LiveStreamOptions.HlsTokenSeconds)));
            stream.ReportViewerCount(watching + 1);
        }

        var token = LiveTokens.SignHls(options.Value.HlsSecret, stream.PublicPath, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<WatchLiveResponse>.Success(new WatchLiveResponse(
            LiveTokens.PlaybackUrl(options.Value, stream.PublicPath, token),
            now.AddSeconds(LiveStreamOptions.HlsTokenSeconds)));
    }
}

public sealed record CheckHlsTokenRequest(bool FromLoopback, string? OriginalUri, string? HeaderToken, string? CookieToken);

public sealed record CheckHlsTokenResponse;

public sealed class CheckHlsTokenHandler(IApplicationDbContext db, IClock clock, IOptions<LiveStreamOptions> options)
    : IHandler<CheckHlsTokenRequest, Result<CheckHlsTokenResponse>>
{
    public async Task<Result<CheckHlsTokenResponse>> Handle(CheckHlsTokenRequest request, CancellationToken cancellationToken)
    {
        if (!request.FromLoopback)
        {
            return Refuse(true, "live.hls_forbidden");
        }

        var now = clock.UtcNow;
        var path = string.IsNullOrWhiteSpace(request.OriginalUri) ? null : LiveTokens.PathFromUri(request.OriginalUri);
        var token = LiveTokens.PresentedToken(request.HeaderToken, request.OriginalUri, request.CookieToken);
        if (LiveTokens.HlsRefusal(options.Value.HlsSecret, path, token, now) is { } refusal)
        {
            return Refuse(false, refusal);
        }

        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == path, cancellationToken);
        if (stream is null)
        {
            return Refuse(false, "live.hls_unknown_stream");
        }

        if (stream.HiddenByStaff)
        {
            return Refuse(true, "live.hls_forbidden");
        }

        if (stream.Status == LiveStreamStatus.Waiting && stream.PublishKeyUsedAt is not null)
        {
            stream.MarkLive(now);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (stream.Status != LiveStreamStatus.Live)
        {
            return Refuse(true, "live.hls_forbidden");
        }

        return Result<CheckHlsTokenResponse>.Success(new CheckHlsTokenResponse());
    }

    private static Result<CheckHlsTokenResponse> Refuse(bool forbidden, string code) =>
        Result<CheckHlsTokenResponse>.Failure(forbidden
            ? Error.Forbidden(code, "Esta transmisión no se puede ver.")
            : Error.Unauthorized(code, "El token de la transmisión no es válido."));
}

public sealed record ReportLiveRequest(Guid UserId, string PublicId, ComplaintReason Reason, string? Details);

public sealed class ReportLiveHandler(IApplicationDbContext db, IClock clock, IHandler<FileComplaintRequest, Result<FileComplaintResponse>> complaints)
    : IHandler<ReportLiveRequest, Result<FileComplaintResponse>>
{
    public async Task<Result<FileComplaintResponse>> Handle(ReportLiveRequest request, CancellationToken cancellationToken)
    {
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.PublicId, cancellationToken);
        if (viewer is null || stream is null)
        {
            return Result<FileComplaintResponse>.Failure(LiveRules.NotFound());
        }

        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        if (!await ListLiveHandler.VisibleAsync(db, viewer, stream, settings, clock.UtcNow, cancellationToken))
        {
            return Result<FileComplaintResponse>.Failure(LiveRules.NotFound());
        }

        return await complaints.Handle(
            new FileComplaintRequest(request.UserId, ContentTargetType.LiveStream, stream.Id, request.Reason, request.Details),
            cancellationToken);
    }
}

public sealed record AuthorizeLiveRequest(bool FromLoopback, string? Secret, string Action, string Path, string? Password, string? Query);

public sealed class AuthorizeLiveHandler(IApplicationDbContext db, IClock clock, IOptions<LiveStreamOptions> options)
    : IHandler<AuthorizeLiveRequest, Result>
{
    public async Task<Result> Handle(AuthorizeLiveRequest request, CancellationToken cancellationToken)
    {
        if (!request.FromLoopback || !LiveIngress.SecretMatches(options.Value.IngestSecret, request.Secret))
        {
            return Result.Failure(Error.Unauthorized("live.unauthorized", "No autorizado."));
        }

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is "api" or "metrics" or "pprof")
        {
            return Result.Failure(Error.Unauthorized("live.unauthorized", "No autorizado."));
        }

        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.Path, cancellationToken);
        if (stream is null)
        {
            return Reject("live.unknown_path");
        }

        if (stream.HiddenByStaff)
        {
            return Reject("live.invalid_state");
        }

        if (action is "publish")
        {
            var password = PublishPassword(request.Password, request.Query);
            var hash = string.IsNullOrWhiteSpace(password) ? string.Empty : LiveTokens.HashKey(password);
            if (stream.PublishRefusal(password, hash, clock.UtcNow) is { } refusal)
            {
                return Reject(refusal);
            }

            if (!stream.AcceptPublishKey(hash, clock.UtcNow))
            {
                return Reject("live.invalid_state");
            }

            stream.MarkLive(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        if (action is "read" or "playback")
        {
            if (stream.Status == LiveStreamStatus.Expired)
            {
                return Reject("live.limit");
            }

            if (stream.Status == LiveStreamStatus.Waiting && stream.PublishKeyUsedAt is not null && stream.MarkLive(clock.UtcNow))
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            if (stream.Status != LiveStreamStatus.Live)
            {
                return Reject("live.invalid_state");
            }

            var token = QueryValue(request.Query, "token");
            if (token is not null && !LiveTokens.VerifyHls(options.Value.HlsSecret, request.Path, token, clock.UtcNow))
            {
                return Reject("live.invalid_key");
            }

            return Result.Success();
        }

        return Reject("live.unauthorized");
    }

    internal static string? PublishPassword(string? password, string? query)
    {
        if (!string.IsNullOrWhiteSpace(password))
        {
            return password;
        }

        return QueryValue(query, "pass");
    }

    private static Result Reject(string code) =>
        Result.Failure(Error.Unauthorized(code, Reason(code)));

    private static string Reason(string code) => code switch
    {
        "live.missing_key" => "Falta la clave de publicación.",
        "live.invalid_key" => "La clave de publicación no es válida.",
        "live.expired" => "La clave de publicación venció.",
        "live.already_used" => "La clave de publicación ya se usó.",
        "live.unknown_path" => "No hay una transmisión con esa ruta.",
        "live.invalid_state" => "La transmisión no está en un estado que acepte esta acción.",
        "live.limit" => "La transmisión llegó a su límite.",
        _ => "No autorizado.",
    };

    private static string? QueryValue(string? query, string name)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == name)
            {
                return Uri.UnescapeDataString(pair[1].Replace("+", " ", StringComparison.Ordinal));
            }
        }

        return null;
    }
}

public sealed record ReconcileLiveRequest;

public sealed record ReconcileLiveResponse(int MarkedLive, int Ended);

public sealed class ReconcileLiveHandler(
    IApplicationDbContext db,
    IClock clock,
    IMediaMtxControl media)
    : IHandler<ReconcileLiveRequest, Result<ReconcileLiveResponse>>
{
    public async Task<Result<ReconcileLiveResponse>> Handle(ReconcileLiveRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        var maxMinutes = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.LiveMaxMinutes, 30), 1, 180);
        var paths = await media.ListPathsAsync(cancellationToken);
        var ready = paths.Where(item => item.Ready).Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var known = paths.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var streams = await db.LiveStreams
            .Where(item => item.Status == LiveStreamStatus.Waiting || item.Status == LiveStreamStatus.Live)
            .ToListAsync(cancellationToken);
        var marked = 0;
        var ended = 0;
        foreach (var stream in streams)
        {
            var path = stream.PublicPath ?? string.Empty;
            var publishing = ready.Contains(path);
            if (stream.Status == LiveStreamStatus.Live && stream.StartedAt.AddMinutes(maxMinutes) <= now)
            {
                await media.KickPathAsync(path, cancellationToken);
                stream.End(stream.RecordingPath, now);
                ended++;
                continue;
            }

            if (stream.Status == LiveStreamStatus.Waiting && publishing && stream.MarkLive(now))
            {
                marked++;
                continue;
            }

            if (stream.Status == LiveStreamStatus.Live && !publishing && !known.Contains(path))
            {
                if (now - stream.StartedAt < TimeSpan.FromSeconds(15))
                {
                    continue;
                }

                stream.End(stream.RecordingPath, now);
                ended++;
                continue;
            }

            var keyExpired = stream.PublishKeyExpiresAt is { } expires && now > expires;
            if (stream.Status == LiveStreamStatus.Waiting && keyExpired && !publishing)
            {
                stream.End(null, now);
                ended++;
            }
        }

        var stale = await db.LiveViewerSessions.Where(item => item.ExpiresAt <= now).ToListAsync(cancellationToken);
        db.LiveViewerSessions.RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);
        return Result<ReconcileLiveResponse>.Success(new ReconcileLiveResponse(marked, ended));
    }
}

public sealed record AdminLiveItem(Guid StreamId, string PublicId, string Title, string Visibility, string Status, bool HiddenByStaff, int Viewers, DateTimeOffset StartedAt);

public sealed record ListAdminLiveRequest(Guid StaffId);

public sealed record ListAdminLiveResponse(IReadOnlyList<AdminLiveItem> Streams);

public sealed class ListAdminLiveHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListAdminLiveRequest, Result<ListAdminLiveResponse>>
{
    public async Task<Result<ListAdminLiveResponse>> Handle(ListAdminLiveRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.LiveCut, cancellationToken))
        {
            return Result<ListAdminLiveResponse>.Failure(InteractionErrors.StaffForbidden());
        }

        var now = clock.UtcNow;
        var streams = await db.LiveStreams
            .Where(item => item.Status == LiveStreamStatus.Waiting || item.Status == LiveStreamStatus.Live)
            .ToListAsync(cancellationToken);
        var items = new List<AdminLiveItem>();
        foreach (var stream in streams)
        {
            var viewers = await db.LiveViewerSessions.CountAsync(item => item.LiveStreamId == stream.Id && item.ExpiresAt > now, cancellationToken);
            items.Add(new AdminLiveItem(
                stream.Id,
                stream.PublicPath ?? string.Empty,
                stream.Title,
                stream.Visibility.ToString(),
                stream.Status.ToString(),
                stream.HiddenByStaff,
                viewers,
                stream.StartedAt));
        }

        return Result<ListAdminLiveResponse>.Success(new ListAdminLiveResponse(items));
    }
}

public sealed record HideLiveRequest(Guid StaffId, string PublicId, string Reason);

public sealed class HideLiveHandler(IApplicationDbContext db, IClock clock, IMediaMtxControl media)
    : IHandler<HideLiveRequest, Result>
{
    public async Task<Result> Handle(HideLiveRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.LiveCut, cancellationToken))
        {
            return Result.Failure(InteractionErrors.StaffForbidden());
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 8)
        {
            return Result.Failure(Error.Validation("live.reason_required", "Escribe por qué se oculta la transmisión."));
        }

        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.PublicId, cancellationToken);
        if (stream is null)
        {
            return Result.Failure(LiveRules.NotFound());
        }

        var now = clock.UtcNow;
        if (stream.PublicPath is { } path)
        {
            await media.KickPathAsync(path, cancellationToken);
        }

        stream.Hide(request.Reason.Trim(), now);
        Audit.Write(db, request.StaffId, "live.hide", "LiveStream", stream.Id, null, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListLiveComplaintsRequest(Guid StaffId, string PublicId);

public sealed class ListLiveComplaintsHandler(IApplicationDbContext db)
    : IHandler<ListLiveComplaintsRequest, Result<ListComplaintsResponse>>
{
    public async Task<Result<ListComplaintsResponse>> Handle(ListLiveComplaintsRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.ComplaintsResolve, cancellationToken))
        {
            return Result<ListComplaintsResponse>.Failure(InteractionErrors.StaffForbidden());
        }

        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == request.PublicId, cancellationToken);
        if (stream is null)
        {
            return Result<ListComplaintsResponse>.Failure(LiveRules.NotFound());
        }

        var rows = await db.Complaints
            .Where(item => item.TargetType == ContentTargetType.LiveStream && item.TargetId == stream.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var reporterIds = rows.Select(item => item.ReporterId).Distinct().ToArray();
        var names = await db.Users.Where(item => reporterIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.UserName.Value, cancellationToken);
        var items = rows.Select(item => new ComplaintQueueItem(
            item.Id,
            item.TargetType.ToString(),
            item.TargetId,
            item.Reason.ToString(),
            item.Status.ToString(),
            item.IsPriority,
            item.CreatedAt,
            names.GetValueOrDefault(item.ReporterId) ?? "Situero")).ToList();
        return Result<ListComplaintsResponse>.Success(new ListComplaintsResponse(items));
    }
}
