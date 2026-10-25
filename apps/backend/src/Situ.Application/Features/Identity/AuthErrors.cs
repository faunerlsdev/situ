using Situ.Application.Common.Results;

namespace Situ.Application.Features.Identity;

internal static class AuthErrors
{
    public const string InvalidCredentialsCode = "auth.invalid_credentials";

    public const string InvalidCredentialsMessage = "Correo, usuario o contraseña incorrectos.";

    public static Error InvalidCredentials() => Error.Unauthorized(InvalidCredentialsCode, InvalidCredentialsMessage);

    public static Error Locked(DateTimeOffset retryAfter) =>
        Error.Locked("auth.locked", "Demasiados intentos. Espera 15 minutos e inténtalo de nuevo.", retryAfter);

    public static Error DeviceLimit(string accessToken, DateTimeOffset expiresAt) =>
        new(
            "auth.device_limit",
            "Llegaste al límite de dispositivos de tu plan.",
            ErrorType.Forbidden,
            new Dictionary<string, object?>
            {
                ["accessToken"] = accessToken,
                ["accessTokenExpiresAt"] = expiresAt,
                ["scope"] = "device_management",
            });

    public static Error PendingGuardian() =>
        Error.Forbidden("auth.pending_guardian", "Esta cuenta espera la autorización de un acudiente.");

    public static Error GuardianConsentRequired(string accessToken, DateTimeOffset expiresAt) =>
        new(
            "auth.guardian_consent_required",
            "Necesitamos la autorización de tu representante legal.",
            ErrorType.Forbidden,
            new Dictionary<string, object?>
            {
                ["accessToken"] = accessToken,
                ["accessTokenExpiresAt"] = expiresAt,
                ["scope"] = "guardian_consent",
            });

    public static Error EmailUnconfirmed() =>
        Error.Forbidden("auth.email_unconfirmed", "Tu correo todavía no está confirmado.");

    public static Error Suspended() =>
        Error.Forbidden("auth.suspended", "Esta cuenta está suspendida.");

    public static Error NotAllowed() =>
        Error.Forbidden("auth.not_allowed", "Esta cuenta está bloqueada y no puede ingresar.");

    public static Error SessionInvalid() =>
        Error.Unauthorized("auth.session_invalid", "La sesión ya no es válida. Vuelve a ingresar.");

    public static Error Unauthenticated() =>
        Error.Unauthorized("auth.unauthenticated", "Inicia sesión para continuar.");
}
