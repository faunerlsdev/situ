using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship;

internal static class GuardianConsentMail
{
    public static (string Subject, string Body) Verification(string guardianName, string verificationUrl)
    {
        var body = $"""
            Hola {guardianName},

            Para autorizar la cuenta de un menor en SITU confirma este correo:

            {verificationUrl}

            El enlace vence en 24 horas y solo se puede usar una vez. Si no reconoces esta solicitud, ignórala.

            SITU
            """;

        return ("Confirma tu correo para autorizar la cuenta", body);
    }

    public static (string Subject, string Body) Receipt(
        string guardianName,
        string minorUserName,
        string documentType,
        string documentNumber,
        string contactEmail,
        bool liveStreamingAuthorized,
        bool locationSharingAuthorized,
        string noticeVersion,
        string signature,
        DateTimeOffset authorizedAt)
    {
        var body = $"""
            Hola {guardianName},

            Esta es la constancia de que autorizaste la cuenta de {minorUserName} en SITU.

            Documento: {documentType} {documentNumber}
            Correo verificado: {contactEmail}
            Transmisión en vivo: {YesNo(liveStreamingAuthorized)}
            Compartir ubicación con amigos: {YesNo(locationSharingAuthorized)}
            Aviso de privacidad: {noticeVersion}
            Fecha: {authorizedAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC
            Firma: {signature}

            Guarda este mensaje. Si no diste esta autorización, escribe a SITU.

            SITU
            """;

        return ("Constancia de autorización en SITU", body);
    }

    public static string DocumentLabel(GuardianDocumentType documentType) => documentType switch
    {
        GuardianDocumentType.CitizenshipCard => "Cédula de ciudadanía",
        GuardianDocumentType.ForeignerCard => "Cédula de extranjería",
        GuardianDocumentType.Passport => "Pasaporte",
        _ => "Otro documento",
    };

    private static string YesNo(bool value) => value ? "sí" : "no";
}
