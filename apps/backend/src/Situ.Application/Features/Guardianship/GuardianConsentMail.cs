using Situ.Application.Common.Mail;
using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship;

internal static class GuardianConsentMail
{
    public static (string Subject, string Body) Request(string reviewUrl)
    {
        var body = BrandedEmail.Render(
            "Un menor pidió tu autorización para usar SITU.",
            "Hola,",
            [
                "Un menor quiere usar SITU y escribió tu correo para que autorices la cuenta.",
                "El enlace vence en 7 días y solo se puede usar una vez. Si no conoces esta solicitud, ignórala.",
            ],
            new EmailButton("Revisar la solicitud", reviewUrl));

        return ("Autoriza una cuenta de SITU", body);
    }

    public static (string Subject, string Body) Verification(string guardianName, string verificationUrl, string? code = null)
    {
        var paragraphs = new List<string>
        {
            "Para autorizar la cuenta de un menor en SITU confirma este correo.",
            "El enlace vence en 24 horas y solo se puede usar una vez. Si no reconoces esta solicitud, ignórala.",
        };
        if (!string.IsNullOrWhiteSpace(code))
        {
            paragraphs.Add($"También puedes escribir este código en la página: {code}. Vence en 10 minutos.");
        }

        var body = BrandedEmail.Render(
            "Confirma tu correo para autorizar la cuenta.",
            $"Hola {guardianName},",
            paragraphs,
            new EmailButton("Confirmar correo", verificationUrl));

        return ("Confirma tu correo para autorizar la cuenta", body);
    }

    public static (string Subject, string Body) VerificationCode(string code)
    {
        var body = BrandedEmail.Render(
            "Tu código para continuar la autorización.",
            "Hola,",
            [
                $"Tu código es {code}.",
                "Vence en 10 minutos. Si no pediste este código, ignora el mensaje.",
            ]);
        return ("Código para autorizar una cuenta en SITU", body);
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
        DateTimeOffset authorizedAt,
        string? manageUrl = null)
    {
        var paragraphs = new List<string>
        {
            $"Esta es la constancia de que autorizaste la cuenta de {minorUserName} en SITU.",
            "Guarda este mensaje. Si no diste esta autorización, escribe a SITU.",
        };
        if (!string.IsNullOrWhiteSpace(manageUrl))
        {
            paragraphs.Add("Desde el enlace puedes revocar la autorización o cambiar los permisos de transmisión y de ubicación.");
        }

        var body = BrandedEmail.Render(
            "Constancia de la autorización que registraste.",
            $"Hola {guardianName},",
            paragraphs,
            string.IsNullOrWhiteSpace(manageUrl) ? null : new EmailButton("Gestionar la autorización", manageUrl),
            lines:
            [
                $"Documento: {documentType} {documentNumber}",
                $"Correo verificado: {contactEmail}",
                $"Transmisión en vivo: {YesNo(liveStreamingAuthorized)}",
                $"Compartir ubicación con amigos: {YesNo(locationSharingAuthorized)}",
                $"Aviso de privacidad: {noticeVersion}",
                $"Fecha: {authorizedAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC",
                $"Firma: {signature}",
            ]);

        return ("Constancia de autorización en SITU", body);
    }

    public static string DocumentLabel(GuardianDocumentType documentType) => documentType switch
    {
        GuardianDocumentType.CitizenshipCard => "Cédula de ciudadanía",
        GuardianDocumentType.ForeignerCard => "Cédula de extranjería",
        GuardianDocumentType.Passport => "Pasaporte",
        _ => "Otro documento",
    };

    public static (string Subject, string Body) MinorAuthorized() =>
        Plain(
            "Tu representante autorizó la cuenta",
            "Tu cuenta ya está autorizada.",
            ["Tu mamá, papá o representante legal autorizó que uses SITU.", "Ingresa de nuevo para entrar."]);

    public static (string Subject, string Body) MinorDeclined() =>
        Plain(
            "Tu representante no autorizó la cuenta",
            "La autorización no se concedió.",
            ["Tu representante no autorizó la cuenta. No podrás entrar a SITU."]);

    public static (string Subject, string Body) MinorExpired() =>
        Plain(
            "La autorización venció",
            "Pasaron los 7 días.",
            ["Tu representante no respondió a tiempo. Si quieres, puedes pedir una nueva autorización al ingresar."]);

    public static (string Subject, string Body) GuardianExpired() =>
        Plain(
            "La solicitud de autorización venció",
            "La solicitud ya no está vigente.",
            ["Pasaron 7 días y la solicitud de autorización en SITU se cerró. No tienes que hacer nada más."]);

    public static (string Subject, string Body) Revoked(string minorUserName) =>
        Plain(
            "Se revocó una autorización en SITU",
            "La autorización quedó revocada.",
            [$"La autorización de la cuenta de {minorUserName} quedó revocada. El menor no podrá usar SITU hasta que un representante la autorice de nuevo."]);

    private static (string Subject, string Body) Plain(string subject, string preheader, IReadOnlyList<string> paragraphs)
    {
        var body = BrandedEmail.Render(preheader, "Hola,", paragraphs);
        return (subject, body);
    }

    private static string YesNo(bool value) => value ? "sí" : "no";
}
