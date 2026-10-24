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

    public static Error NotPending() =>
        Error.Forbidden("guardian_consent.not_pending", "Esta cuenta no está esperando la autorización de un representante.");

    public static Error NoticeRequired() =>
        Error.Validation("guardian_consent.notice_required", "Antes de enviar la solicitud, lee la explicación y marca Entendido.");

    public static Error GuardianEmailRejected() =>
        Error.Validation("guardian_consent.email_rejected", "Escribe el correo de tu representante. Debe ser distinto al tuyo.");

    public static Error NoOpenRequest() =>
        Error.Validation("guardian_consent.no_open_request", "No hay una solicitud esperando respuesta.");

    public static Error AlreadyOpen() =>
        Error.Conflict("guardian_consent.already_open", "Ya hay una solicitud esperando respuesta.");

    public static Error Declined() =>
        Error.Forbidden("guardian_consent.declined", "Tu representante no autorizó la cuenta.");

    public static Error SendLimit(DateTimeOffset retryAfter) =>
        Error.RateLimited(
            "guardian_consent.send_limited",
            "Llegaste al límite de envíos. Podrás intentarlo de nuevo más tarde.",
            retryAfter);

    public static Error CodeRejected() =>
        Error.Validation("guardian_consent.code_invalid", "El código no es válido. Revisa el correo e inténtalo de nuevo.");

    public static Error DataProcessingRequired() =>
        Error.Validation("guardian_consent.data_processing_required", "Para autorizar, acepta el tratamiento de los datos.");

    public static Error LegalHold() =>
        Error.Conflict("guardian_consent.legal_hold", "No pudimos eliminar el registro en este momento.");
}
