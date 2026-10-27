using Microsoft.Extensions.Options;
using Situ.Infrastructure.Guardianship;
using Situ.Infrastructure.Reports;

namespace Situ.Api.Configuration;

public static class ProductionSecretRules
{
    public const string JwtRejectedMessage =
        "Jwt:Secret es un placeholder o coincide con una clave de desarrollo.";

    public const string GuardianRejectedMessage =
        "GuardianConsent:SigningKey es un placeholder o coincide con la clave de desarrollo.";

    public const string MediaRejectedMessage =
        "FileStorage:SigningKey falta, es un placeholder o coincide con la clave de desarrollo.";

    public const string MediaRootMessage =
        "Falta FileStorage:Root. En producción define FileStorage__Root.";

    public const string DistinctSecretsMessage =
        "Jwt:Secret, GuardianConsent:SigningKey y FileStorage:SigningKey deben ser distintas.";

    public const string ExpoRejectedMessage =
        "Falta Notifications:ExpoAccessToken o es un placeholder. En producción Notifications__RequireExpoAccessToken debe ser true y el token tiene que ser el de Expo.";

    public const string CaptchaRejectedMessage =
        "Captcha:SecretKey falta, es un placeholder o el proveedor es Development. En producción usa Turnstile.";

    public const string CaptchaSiteMessage =
        "Falta Captcha:SiteKey.";

    public const string PublicUrlMessage =
        "PublicBaseUrl debe ser un origen https y no puede ser localhost.";

    public const string EmailHostMessage =
        "Email:Host no puede estar vacío, ser localhost ni usar el puerto 1025 en Production.";

    public const string EmailFromMessage =
        "Email:FromAddress no puede ser no-reply@situ.local en Production.";

    public const string EmailPasswordMessage =
        "Email:Password falta o es un placeholder.";

    public const string ConnectionRejectedMessage =
        "ConnectionStrings:Default contiene un placeholder.";

    public const string CorsRejectedMessage =
        "Cors:AllowedOrigins debe incluir al menos un origen https que no sea localhost.";

    public static bool TryValidate(IConfiguration configuration, out string? error)
    {
        var errors = new List<string>();
        var jwt = configuration["Jwt:Secret"] ?? "";
        var guardian = configuration["GuardianConsent:SigningKey"] ?? "";
        var media = configuration["FileStorage:SigningKey"] ?? "";

        if (IsRejectedSecret(jwt) || IsDevelopmentKey(jwt))
        {
            errors.Add(JwtRejectedMessage);
        }

        if (IsRejectedSecret(guardian) || string.Equals(guardian, GuardianConsentOptions.DevelopmentSigningKey, StringComparison.Ordinal))
        {
            errors.Add(GuardianRejectedMessage);
        }

        if (string.IsNullOrWhiteSpace(media) || IsRejectedSecret(media) || string.Equals(media, FileStorageOptions.DevelopmentSigningKey, StringComparison.Ordinal))
        {
            errors.Add(MediaRejectedMessage);
        }

        if (string.IsNullOrWhiteSpace(configuration["FileStorage:Root"]))
        {
            errors.Add(MediaRootMessage);
        }

        if (!string.IsNullOrWhiteSpace(jwt)
            && !string.IsNullOrWhiteSpace(guardian)
            && !string.IsNullOrWhiteSpace(media)
            && (string.Equals(jwt, guardian, StringComparison.Ordinal)
                || string.Equals(jwt, media, StringComparison.Ordinal)
                || string.Equals(guardian, media, StringComparison.Ordinal)))
        {
            errors.Add(DistinctSecretsMessage);
        }

        if (RequireExpo(configuration) && IsRejectedSecret(configuration["Notifications:ExpoAccessToken"], 20))
        {
            errors.Add(ExpoRejectedMessage);
        }

        var captchaProvider = configuration["Captcha:Provider"];
        if (string.Equals(captchaProvider, "Development", StringComparison.OrdinalIgnoreCase)
            || IsRejectedSecret(configuration["Captcha:SecretKey"], 16))
        {
            errors.Add(CaptchaRejectedMessage);
        }

        if (string.IsNullOrWhiteSpace(configuration["Captcha:SiteKey"]))
        {
            errors.Add(CaptchaSiteMessage);
        }

        if (!IsPublicHttpsOrigin(First(configuration["PublicBaseUrl"], configuration["Email:ApiPublicBaseUrl"])))
        {
            errors.Add(PublicUrlMessage);
        }

        if (!IsProductionSmtp(configuration["Email:Host"], configuration["Email:Port"]))
        {
            errors.Add(EmailHostMessage);
        }

        var from = configuration["Email:FromAddress"] ?? "";
        if (string.IsNullOrWhiteSpace(from) || string.Equals(from, "no-reply@situ.local", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(EmailFromMessage);
        }

        if (IsRejectedSecret(configuration["Email:Password"], 12))
        {
            errors.Add(EmailPasswordMessage);
        }

        if (IsPlaceholderConnection(configuration["ConnectionStrings:Default"]))
        {
            errors.Add(ConnectionRejectedMessage);
        }

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => !IsPublicHttpsOrigin(origin)))
        {
            errors.Add(CorsRejectedMessage);
        }

        if (errors.Count == 0)
        {
            error = null;
            return true;
        }

        error = string.Join(' ', errors);
        return false;
    }

    public static bool IsRejectedSecret(string? value, int minimumLength = 32)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimumLength)
        {
            return true;
        }

        var normalized = value.Trim();
        var lower = normalized.ToLowerInvariant();
        if (lower is "secret" or "password" or "changeme" or "change_me" or "change-me" or "placeholder" or "todo" or "xxx" or "tu_clave" or "dev" or "development" or "replace-me" or "replace_me")
        {
            return true;
        }

        return lower.Contains("change_me", StringComparison.Ordinal)
            || lower.Contains("changeme", StringComparison.Ordinal)
            || lower.Contains("change-me", StringComparison.Ordinal)
            || lower.Contains("placeholder", StringComparison.Ordinal)
            || lower.Contains("tu_clave", StringComparison.Ordinal)
            || lower.Contains("replace-me", StringComparison.Ordinal)
            || lower.Contains("situ-dev", StringComparison.Ordinal)
            || lower.Contains("situ-development", StringComparison.Ordinal);
    }

    private static bool IsDevelopmentKey(string value) =>
        string.Equals(value, GuardianConsentOptions.DevelopmentSigningKey, StringComparison.Ordinal)
        || string.Equals(value, FileStorageOptions.DevelopmentSigningKey, StringComparison.Ordinal);

    private static bool RequireExpo(IConfiguration configuration)
    {
        var raw = configuration["Notifications:RequireExpoAccessToken"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return !bool.TryParse(raw, out var required) || required;
    }

    private static bool IsProductionSmtp(string? host, string? port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().TrimEnd('.');
        if (normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized is "127.0.0.1" or "::1")
        {
            return false;
        }

        return port != "1025";
    }

    private static bool IsPublicHttpsOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.HostNameType is UriHostNameType.Unknown)
        {
            return false;
        }

        return uri.Host is not ("localhost" or "127.0.0.1" or "::1");
    }

    private static bool IsPlaceholderConnection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var lower = value.ToLowerInvariant();
        return lower.Contains("change_me", StringComparison.Ordinal)
            || lower.Contains("changeme", StringComparison.Ordinal)
            || lower.Contains("placeholder", StringComparison.Ordinal)
            || lower.Contains("tu_clave", StringComparison.Ordinal)
            || lower.Contains("password=password", StringComparison.Ordinal)
            || lower.Contains("password=secret", StringComparison.Ordinal);
    }

    private static string? First(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}

public sealed class ProductionGate;

public sealed class ProductionGateValidator(IHostEnvironment environment, IConfiguration configuration) : IValidateOptions<ProductionGate>
{
    public ValidateOptionsResult Validate(string? name, ProductionGate options)
    {
        if (!environment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        return ProductionSecretRules.TryValidate(configuration, out var error)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(error!);
    }
}
