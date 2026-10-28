using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Social;

namespace Situ.Application.Features.Live;

public sealed class LiveStreamOptions
{
    public const string SectionName = "Live";
    public const int PublishKeySeconds = 120;
    public const int HlsTokenSeconds = 120;
    public const int HlsTokenMaxSeconds = 180;

    public string Domain { get; set; } = "somossitu.com";

    public int RtmpsPort { get; set; } = 1936;

    public string IngestSecret { get; set; } = "";

    public string HlsSecret { get; set; } = "";

    public string ControlUrl { get; set; } = "http://127.0.0.1:19998";
}

public interface IMediaMtxControl
{
    Task<IReadOnlyList<MediaMtxPath>> ListPathsAsync(CancellationToken cancellationToken);

    Task<bool> KickPathAsync(string path, CancellationToken cancellationToken);
}

public sealed record MediaMtxPath(string Name, bool Ready);

public interface ILiveNotifier
{
    Task ChangedAsync(IReadOnlyCollection<string> groups, string publicId, string change, CancellationToken cancellationToken);
}

public static class LiveTokens
{
    public static string NewPath() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string NewPublishKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string SignHls(string secret, string path, DateTimeOffset now, int ttlSeconds = LiveStreamOptions.HlsTokenSeconds)
    {
        if (string.IsNullOrWhiteSpace(secret) || ttlSeconds < 1 || ttlSeconds > LiveStreamOptions.HlsTokenMaxSeconds)
        {
            throw new InvalidOperationException("HLS token cannot be signed.");
        }

        var expires = now.ToUnixTimeSeconds() + ttlSeconds;
        var mac = Mac(secret, expires, path);
        return $"{expires}.{mac}";
    }

    public static bool VerifyHls(string secret, string path, string? token, DateTimeOffset now) =>
        HlsRefusal(secret, path, token, now) is null;

    /// <summary>
    /// Why an HLS token cannot be used. Null means the signature matches this path and the clock.
    /// A well-formed token that does not match the path is a path mismatch: the MAC is bound to the id.
    /// </summary>
    public static string? HlsRefusal(string secret, string? path, string? token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "live.hls_path_mismatch";
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return "live.hls_missing_token";
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return "live.hls_bad_signature";
        }

        var parts = token.Trim().Split('.', 2);
        if (parts.Length != 2
            || !long.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var expires)
            || parts[1].Length != 64
            || !IsLowerHex(parts[1]))
        {
            return "live.hls_bad_signature";
        }

        var nowSeconds = now.ToUnixTimeSeconds();
        if (expires < nowSeconds || expires - nowSeconds > LiveStreamOptions.HlsTokenMaxSeconds)
        {
            return "live.hls_expired";
        }

        var expected = Mac(secret, expires, path);
        return Fixed(expected, parts[1]) ? null : "live.hls_path_mismatch";
    }

    public static string PublishUrl(LiveStreamOptions options, string path, string publishKey)
    {
        var user = Uri.EscapeDataString(PublishUser);
        var pass = Uri.EscapeDataString(publishKey);
        return $"rtmps://live.{options.Domain}:{options.RtmpsPort}/{path}?user={user}&pass={pass}";
    }

    public const string PublishUser = "situ";

    /// <summary>
    /// The token is a path segment, not a query value. AppleCoreMedia resolves
    /// <c>seg.m4s</c> against the playlist directory and drops <c>?token=</c>.
    /// </summary>
    public static string PlaybackUrl(LiveStreamOptions options, string path, string token) =>
        $"https://live.{options.Domain}/hls/{path}/{token}/index.m3u8";

    public static string? PresentedToken(string? header, string? uri, string? cookie)
    {
        if (!string.IsNullOrWhiteSpace(header))
        {
            return header.Trim();
        }

        var fromUri = TokenFromUri(uri);
        if (!string.IsNullOrWhiteSpace(fromUri))
        {
            return fromUri;
        }

        return string.IsNullOrWhiteSpace(cookie) ? null : cookie.Trim();
    }

    public static string? TokenFromUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var split = uri.Split('?', 2);
        if (split.Length == 2)
        {
            var queryToken = QueryToken(split[1]);
            if (!string.IsNullOrWhiteSpace(queryToken))
            {
                return queryToken;
            }
        }

        var path = split[0];
        const string marker = "/hls/";
        var start = path.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var parts = path[(start + marker.Length)..].Split('/');
        if (parts.Length >= 2 && IsTokenShape(parts[1]))
        {
            return parts[1];
        }

        return null;
    }

    public static string? PathFromUri(string uri)
    {
        var path = uri.Split('?', 2)[0];
        const string marker = "/hls/";
        var start = path.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var rest = path[(start + marker.Length)..];
        var slash = rest.IndexOf('/');
        var name = slash < 0 ? rest : rest[..slash];
        return name.Length is > 0 and <= 64 ? name : null;
    }

    private static bool IsTokenShape(string value)
    {
        var parts = value.Split('.', 2);
        return parts.Length == 2
            && parts[0].Length is > 0 and <= 12
            && parts[1].Length == 64
            && IsDigits(parts[0])
            && IsLowerHex(parts[1]);
    }

    private static bool IsDigits(string value)
    {
        foreach (var letter in value)
        {
            if (letter is < '0' or > '9')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var letter in value)
        {
            if (letter is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static string? QueryToken(string query)
    {
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "token")
            {
                return Uri.UnescapeDataString(pair[1].Replace("+", " ", StringComparison.Ordinal));
            }
        }

        return null;
    }

    private static string Mac(string secret, long expires, string path)
    {
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{expires}.{path}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool Fixed(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        var difference = 0;
        for (var index = 0; index < left.Length; index++)
        {
            difference |= left[index] ^ right[index];
        }

        return difference == 0;
    }
}

public static class LiveLocation
{
    public static GeoPoint Round(GeoPoint raw, int precisionMeters)
    {
        var meters = Math.Clamp(precisionMeters, 1, 5000);
        var latStep = meters / 111_320d;
        var cos = Math.Cos(raw.Latitude * Math.PI / 180d);
        var lonStep = meters / (111_320d * Math.Max(Math.Abs(cos), 0.01));
        var latitude = Math.Round(raw.Latitude / latStep) * latStep;
        var longitude = Math.Round(raw.Longitude / lonStep) * lonStep;
        latitude = Math.Clamp(latitude, -90, 90);
        longitude = Math.Clamp(longitude, -180, 180);
        return new GeoPoint(latitude, longitude);
    }
}

public static class LiveIngress
{
    public static bool IsLoopback(System.Net.IPAddress? address) =>
        address is not null && System.Net.IPAddress.IsLoopback(address);

    public static bool SecretMatches(string configured, string? provided)
    {
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(provided) || configured.Length != provided.Length)
        {
            return false;
        }

        var difference = 0;
        for (var index = 0; index < configured.Length; index++)
        {
            difference |= configured[index] ^ provided[index];
        }

        return difference == 0;
    }
}

public static class LiveAudience
{
    public static bool CanWatch(
        User viewer,
        User streamer,
        LiveStream stream,
        bool areFriends,
        bool blocked,
        bool minorsCanStream,
        bool allowAdultMinor,
        DateTimeOffset now,
        bool ignoreHidden = false)
    {
        if ((stream.HiddenByStaff && !ignoreHidden) || blocked || stream.Status is not (LiveStreamStatus.Live or LiveStreamStatus.Ended or LiveStreamStatus.Cut))
        {
            return false;
        }

        if (!MinorProtectionPolicy.CanStreamTo(streamer.AgeOn(now), stream.Visibility))
        {
            return false;
        }

        if (streamer.IsMinor(now) && !minorsCanStream)
        {
            return false;
        }

        if (!MinorProtectionPolicy.CanBeFriends(viewer.AgeOn(now), streamer.AgeOn(now), allowAdultMinor))
        {
            areFriends = false;
        }

        if (viewer.Id == streamer.Id)
        {
            return true;
        }

        if (viewer.IsMinor(now))
        {
            return areFriends
                && streamer.IsMinor(now)
                && MinorProtectionPolicy.CanBeFriends(viewer.AgeOn(now), streamer.AgeOn(now), allowAdultMinor);
        }

        if (stream.Visibility == ContentVisibility.FriendsOnly)
        {
            return areFriends;
        }

        return stream.Visibility == ContentVisibility.Public;
    }
}

internal static class LiveRules
{
    public static async Task<IReadOnlyDictionary<string, string>> SettingsAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await ReportSettings.LoadAsync(db, cancellationToken);

    public static bool Flag(IReadOnlyDictionary<string, string> settings, string key) =>
        string.Equals(settings.GetValueOrDefault(key), "true", StringComparison.OrdinalIgnoreCase);

    public static async Task<bool> FriendsAsync(IApplicationDbContext db, Guid left, Guid right, CancellationToken cancellationToken) =>
        await db.Friendships.AnyAsync(
            item => item.Status == FriendshipStatus.Accepted
                && ((item.RequesterId == left && item.AddresseeId == right) || (item.RequesterId == right && item.AddresseeId == left)),
            cancellationToken);

    public static async Task<bool> BlockedAsync(IApplicationDbContext db, Guid viewerId, Guid streamerId, CancellationToken cancellationToken) =>
        await db.UserBlocks.AnyAsync(
            item => (item.Scope == UserBlockScope.Full
                    && ((item.BlockerId == viewerId && item.BlockedUserId == streamerId)
                        || (item.BlockerId == streamerId && item.BlockedUserId == viewerId)))
                || (item.Scope == UserBlockScope.AnonymousContentOnly
                    && item.BlockerId == viewerId
                    && item.BlockedUserId == streamerId),
            cancellationToken);

    public static Error NotFound() => Error.NotFound("live.not_found", "No encontramos esa transmisión.");

    public static Error NotConfigured() => Error.Failure("live.not_configured", "La transmisión en vivo no está configurada.");

    public static Error FromDomain(DomainException exception) => exception.Code switch
    {
        "live.minors_cannot_stream" => Error.Forbidden(exception.Code, "En esta versión un menor no puede transmitir."),
        "live.minor_friends_only" => Error.Forbidden(exception.Code, "Un menor solo puede transmitir a sus amigos."),
        _ => Error.Validation(exception.Code, "No se pudo abrir la transmisión."),
    };
}
