using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;

namespace Situ.Application.Features.Profiles;

internal static class ProfileRules
{
    public static async Task<IReadOnlyDictionary<string, string>> SettingsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            SystemSettingKeys.ProfileUserNameChangeDays,
            SystemSettingKeys.ProfileReservedUserNames,
            SystemSettingKeys.ProfileBlockedTerms,
            SystemSettingKeys.ProfileAvatarMaxBytes,
            SystemSettingKeys.LegalTermsUrl,
            SystemSettingKeys.LegalPrivacyUrl,
            SystemSettingKeys.LegalSupportUrl,
            SystemSettingKeys.LoginMaxFailedAttempts,
            SystemSettingKeys.LoginLockoutMinutes,
        };
        var stored = await db.SystemSettings
            .Where(setting => keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            merged[key] = stored.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : SystemSettingKeys.Defaults[key];
        }

        return merged;
    }

    public static int Int(IReadOnlyDictionary<string, string> settings, string key, int fallback) =>
        int.TryParse(settings.GetValueOrDefault(key), out var value) ? value : fallback;

    public static IReadOnlySet<string> List(IReadOnlyDictionary<string, string> settings, string key)
    {
        var raw = settings.GetValueOrDefault(key) ?? "";
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool ContainsBlocked(string? text, IReadOnlySet<string> terms)
    {
        if (string.IsNullOrWhiteSpace(text) || terms.Count == 0)
        {
            return false;
        }

        var folded = text.ToLowerInvariant();
        return terms.Any(term => term.Length >= 3 && folded.Contains(term, StringComparison.Ordinal));
    }

    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at >= email.Length - 1)
        {
            return "***";
        }

        return $"{email[0]}***@{email[(at + 1)..]}";
    }

    public static Error ContentRejected() =>
        Error.Validation("profile.content_not_allowed", "Ese texto no se puede usar en el perfil.");

    public static Error UserNameCoolingDown(DateTimeOffset availableAt) =>
        Error.RateLimited(
            "profile.username_change_limited",
            "Aún no puedes cambiar tu nombre de usuario.",
            availableAt);
}
