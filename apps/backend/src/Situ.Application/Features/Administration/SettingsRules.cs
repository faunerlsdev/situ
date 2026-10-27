using System.Globalization;
using System.Text.RegularExpressions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Configuration;
using Situ.Domain.Administration;

namespace Situ.Application.Features.Administration;

internal static partial class SettingsRules
{
    private static readonly Regex VersionPattern = VersionRegex();
    private static readonly Regex UrlPattern = UrlRegex();

    private static readonly Dictionary<string, Func<string, Result<string>>> Rules = Build();

    public static bool IsSensitive(string key)
    {
        var folded = key.ToLowerInvariant();
        return folded.Contains("secret", StringComparison.Ordinal)
            || folded.Contains("password", StringComparison.Ordinal)
            || folded.Contains("token", StringComparison.Ordinal)
            || folded.Contains("signing", StringComparison.Ordinal)
            || folded.Contains("smtp", StringComparison.Ordinal)
            || folded.Contains("connection", StringComparison.Ordinal)
            || folded.Contains("apikey", StringComparison.Ordinal)
            || folded.Contains("api_key", StringComparison.Ordinal);
    }

    public static bool IsVisible(string key) => !IsSensitive(key);

    public static Result<string> Validate(string key, string value)
    {
        if (IsSensitive(key) || !SystemSettingKeys.Defaults.ContainsKey(key))
        {
            return Result<string>.Failure(Error.Validation("settings.not_editable", "Ese ajuste no se puede cambiar desde el panel."));
        }

        if (key == SystemSettingKeys.ReportCatalogVersion)
        {
            return Result<string>.Failure(Error.Validation("settings.read_only", "La versión del catálogo cambia sola con cada edición."));
        }

        if (!Rules.TryGetValue(key, out var rule))
        {
            return int.TryParse(SystemSettingKeys.Defaults[key], NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? Int(value, 0, 1_000_000_000)
                : Text(value, 1000);
        }

        return rule(value);
    }

    private static Dictionary<string, Func<string, Result<string>>> Build()
    {
        var rules = new Dictionary<string, Func<string, Result<string>>>(StringComparer.Ordinal)
        {
            [SystemSettingKeys.LoginMaxFailedAttempts] = value => Int(value, 3, 10),
            [SystemSettingKeys.LoginLockoutMinutes] = value => Int(value, 5, 120),
            [SystemSettingKeys.DefaultNotificationRadiusMeters] = value => Int(value, 100, 10000),
            [SystemSettingKeys.ProfileUserNameChangeDays] = value => Int(value, 1, 365),
            [SystemSettingKeys.AccountDeletionGraceDays] = value => Int(value, 1, 90),
            [SystemSettingKeys.LegalTermsUrl] = Url,
            [SystemSettingKeys.LegalPrivacyUrl] = Url,
            [SystemSettingKeys.LegalSupportUrl] = Url,
            [SystemSettingKeys.PanelBrowseCopy] = value => Text(value, 80),
            [SystemSettingKeys.PanelReportCopy] = value => Text(value, 80),
            [SystemSettingKeys.AnonymousDisplayName] = value => Text(value, 40),
            [SystemSettingKeys.PanelCopyByCountry] = Copies,
            [SystemSettingKeys.ProfileReservedUserNames] = value => Text(value, 1000),
            [SystemSettingKeys.ProfileBlockedTerms] = value => Text(value, 1000),
            [SystemSettingKeys.PhoneRequiredForMinors] = Bool,
            [SystemSettingKeys.SocialTabsEnabled] = Bool,
            [SystemSettingKeys.WebSessionRefreshDays] = value => Int(value, 1, 365),
            [SystemSettingKeys.WebSessionRememberDays] = value => Int(value, 1, 365),
        };

        foreach (var key in new[]
        {
            SystemSettingKeys.AppIosMinSupportedVersion,
            SystemSettingKeys.AppIosLatestVersion,
            SystemSettingKeys.AppAndroidMinSupportedVersion,
            SystemSettingKeys.AppAndroidLatestVersion,
            SystemSettingKeys.AppWebMinSupportedVersion,
            SystemSettingKeys.AppWebLatestVersion,
        })
        {
            rules[key] = Version;
        }

        return rules;
    }

    private static Result<string> Int(string value, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
        {
            return Result<string>.Failure(Error.Validation("settings.invalid_value", $"El valor tiene que estar entre {min} y {max}."));
        }

        return Result<string>.Success(number.ToString(CultureInfo.InvariantCulture));
    }

    private static Result<string> Bool(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed is not ("true" or "false"))
        {
            return Result<string>.Failure(Error.Validation("settings.invalid_value", "Escribe true o false."));
        }

        return Result<string>.Success(trimmed);
    }

    private static Result<string> Text(string value, int max)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is 0 || trimmed.Length > max)
        {
            return Result<string>.Failure(Error.Validation("settings.invalid_value", "El texto no tiene un tamaño válido."));
        }

        return Result<string>.Success(trimmed);
    }

    private static Result<string> Url(string value)
    {
        var trimmed = value.Trim();
        if (!UrlPattern.IsMatch(trimmed) || trimmed.Length > 300)
        {
            return Result<string>.Failure(Error.Validation("settings.invalid_value", "La dirección tiene que empezar por https://."));
        }

        return Result<string>.Success(trimmed);
    }

    private static Result<string> Version(string value)
    {
        var trimmed = value.Trim();
        if (!VersionPattern.IsMatch(trimmed))
        {
            return Result<string>.Failure(Error.Validation("settings.invalid_value", "La versión tiene que verse como 1.2.3."));
        }

        return Result<string>.Success(trimmed);
    }

    private static Result<string> Copies(string value)
    {
        var parsed = PanelCopies.Parse(value);
        if (parsed.IsFailure)
        {
            return Result<string>.Failure(parsed.Error!);
        }

        return Result<string>.Success(System.Text.Json.JsonSerializer.Serialize(
            parsed.Value!.Select(item => new
            {
                countryCode = item.CountryCode,
                locale = item.Locale,
                browse = item.Browse,
                report = item.Report,
            })));
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^https://\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
