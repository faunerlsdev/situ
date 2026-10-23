using Situ.Application.Common.Results;

namespace Situ.Application.Features.Identity;

internal static class AuthErrors
{
    public const string InvalidCredentialsCode = "auth.invalid_credentials";

    public const string InvalidCredentialsMessage = "Correo, usuario o contraseña incorrectos.";

    public static Error InvalidCredentials() => Error.Unauthorized(InvalidCredentialsCode, InvalidCredentialsMessage);

    public static Error Locked(DateTimeOffset retryAfter) =>
        Error.Locked("auth.locked", "La cuenta está bloqueada por demasiados intentos.", retryAfter);

    public static Error DeviceLimit() =>
        Error.Forbidden("auth.device_limit", "Alcanzaste el número de dispositivos de tu plan.");

    public static Error PendingGuardian() =>
        Error.Forbidden("auth.pending_guardian", "Esta cuenta espera la autorización de un acudiente.");

    public static Error Suspended() =>
        Error.Forbidden("auth.suspended", "Esta cuenta está suspendida.");

    public static Error NotAllowed() =>
        Error.Forbidden("auth.not_allowed", "Esta cuenta no puede ingresar.");

    public static Error SessionInvalid() =>
        Error.Unauthorized("auth.session_invalid", "La sesión ya no es válida. Vuelve a ingresar.");

    public static Error Unauthenticated() =>
        Error.Unauthorized("auth.unauthenticated", "Inicia sesión para continuar.");
}
