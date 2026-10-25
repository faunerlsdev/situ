using Situ.Application.Common.Results;

namespace Situ.Application.Features.Notifications;

internal static class NotificationErrors
{
    public static Error Unauthenticated() => Error.Unauthorized("auth.unauthenticated", "Inicia sesión para continuar.");

    public static Error DeviceRequired() =>
        Error.Conflict("notification.device_required", "Vuelve a ingresar desde este dispositivo para continuar.");

    public static Error DeviceNotFound() => Error.NotFound("notification.device_not_found", "No encontramos este dispositivo.");

    public static Error ProximityOff() =>
        Error.Conflict("notification.proximity_disabled", "Activa las alertas de reportes cercanos para compartir tu zona.");

    public static Error ProximityMinor() =>
        Error.Forbidden("notification.proximity_not_allowed_minor", "Para avisos por cercanía, tu representante tiene que autorizar la ubicación. Mientras tanto te avisamos por tu ciudad.");

    public static Error LocationLimited(DateTimeOffset retryAfter) =>
        Error.RateLimited("notification.alert_location_limited", "Espera un momento antes de actualizar tu zona.", retryAfter);

    public static Error TimeZone() => Error.Validation("notification.timezone_invalid", "Esa zona horaria no es válida.");

    public static Error QuietHours() => Error.Validation("notification.quiet_hours", "El silencio necesita una hora de inicio y una de fin.");

    public static Error CityNotFound() => Error.NotFound("notification.city_not_found", "No encontramos esa ciudad.");

    public static Error HomeCity() => Error.Conflict("notification.interest_city_home", "Esa ya es tu ciudad de interés principal.");

    public static Error CityExists() => Error.Conflict("notification.interest_city_exists", "Ya sigues esa ciudad.");

    public static Error CityLimit() => Error.Conflict("notification.interest_city_limit", "Llegaste al máximo de ciudades de tu plan.");

    public static Error CategoryNotFound() => Error.NotFound("notification.category_not_found", "No encontramos esa categoría.");

    public static Error NotFound() => Error.NotFound("notification.not_found", "No encontramos esa notificación.");
}
