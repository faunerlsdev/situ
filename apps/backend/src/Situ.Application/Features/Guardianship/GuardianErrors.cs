using Situ.Application.Common.Results;
using Situ.Domain.Common;

namespace Situ.Application.Features.Guardianship;

internal static class GuardianErrors
{
    public static Error FromDomain(DomainException exception) => exception.Code switch
    {
        "guardian_consent.expired" => Error.Validation(exception.Code, "Esta solicitud de autorización venció."),
        "guardian_consent.not_open" => Error.Validation(exception.Code, "Esta solicitud ya no está abierta."),
        "guardian_consent.data_missing" => Error.Validation(exception.Code, "Primero completa los datos del acudiente."),
        "guardian_consent.email_not_verified" => Error.Validation(
            exception.Code,
            "Primero verifica el correo del acudiente."),
        "email.invalid" => Error.Validation(exception.Code, "El correo no es válido."),
        _ => Error.Validation(exception.Code, "No pudimos completar la solicitud."),
    };

    public static Error NotFound() =>
        Error.NotFound("guardian_consent.not_found", "El enlace de autorización no es válido.");

    public static Error VerificationNotFound() =>
        Error.NotFound("security_token.not_found", "El enlace de verificación no es válido.");

    public static Error EmailSendFailed() =>
        Error.Failure("email.send_failed", "No pudimos enviar el correo de verificación. Inténtalo de nuevo.");
}
