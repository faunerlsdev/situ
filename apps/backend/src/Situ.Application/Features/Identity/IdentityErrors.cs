using Situ.Application.Common.Results;
using Situ.Domain.Common;

namespace Situ.Application.Features.Identity;

internal static class IdentityErrors
{
    public static Error FromDomain(DomainException exception) => exception.Code switch
    {
        "user.below_minimum_age" => Error.Validation(
            exception.Code,
            "No cumples la edad mínima para registrarte en este país."),
        "user.invalid_date_of_birth" => Error.Validation(
            exception.Code,
            "La fecha de nacimiento no puede ser futura."),
        "security_token.already_used" => TokenAlreadyUsed(),
        "security_token.expired" => TokenExpired(),
        _ => Error.Validation(exception.Code, "No pudimos completar la solicitud."),
    };

    public static Error CountryUnavailable() =>
        Error.Validation("geography.country_unavailable", "Ese país no está disponible para el registro.");

    public static Error RegionUnavailable() =>
        Error.Validation("geography.region_unavailable", "Ese departamento no pertenece al país o no está disponible.");

    public static Error CityUnavailable() =>
        Error.Validation("geography.city_unavailable", "Esa ciudad no pertenece al departamento o no está disponible.");

    public static Error EmailTaken() =>
        Error.Conflict("user.email_taken", "Ya existe una cuenta con ese correo.");

    public static Error UserNameTaken() =>
        Error.Conflict("user.username_taken", "Ese nombre de usuario ya está en uso.");

    public static Error InvalidEmail() =>
        Error.Validation("email.invalid", "El correo no es válido.");

    public static Error InvalidGivenNames() =>
        Error.Validation("person_name.invalid", "Escribe tus nombres. Solo letras, espacios, apóstrofos o guiones.");

    public static Error InvalidSurnames() =>
        Error.Validation("person_name.invalid", "Escribe tus apellidos. Solo letras, espacios, apóstrofos o guiones.");

    public static Error InvalidPhone() =>
        Error.Validation("phone.invalid", "Escribe el teléfono con indicativo, por ejemplo +573001234567.");

    public static Error InvalidUserName() =>
        Error.Validation(
            "username.invalid",
            "El usuario debe tener de 3 a 30 caracteres: letras minúsculas, números, '_' o '.'.");

    public static Error InvalidPassword() =>
        Error.Validation(
            "password.invalid",
            "La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.");

    public static Error DocumentsUnavailable() =>
        Error.Validation("legal.documents_unavailable", "No hay documentos legales vigentes para ese país.");

    public static Error DocumentsNotAccepted() =>
        Error.Validation("legal.documents_not_accepted", "Debes aceptar los documentos legales vigentes.");

    public static Error TokenNotFound() =>
        Error.NotFound("security_token.not_found", "El enlace de confirmación no es válido.");

    public static Error ResetTokenNotFound() =>
        Error.NotFound("security_token.not_found", "El enlace para restablecer la contraseña no es válido.");

    public static Error TokenAlreadyUsed() =>
        Error.Validation("security_token.already_used", "Este enlace ya se usó.");

    public static Error TokenExpired() =>
        Error.Validation("security_token.expired", "Este enlace venció.");

    public static Error CurrentPasswordInvalid() =>
        Error.Unauthorized("password.current_invalid", "La contraseña actual no coincide.");

    public static Error ConfirmationTooSoon() =>
        Error.Validation(
            "auth.confirmation_rate_limited",
            "Espera un momento antes de pedir otro correo de confirmación.");

    public static Error EmailSendFailed() =>
        Error.Failure(
            "email.send_failed",
            "No pudimos enviar el correo de confirmación. Pide otro enlace en un momento.");
}
