using Situ.Application.Common.Mail;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity;

internal static class PasswordResetMail
{
    public static (string Subject, string Body) Link(string userName, string resetUrl)
    {
        var minutes = (int)PasswordResetPolicy.TokenLifetime.TotalMinutes;
        var body = BrandedEmail.Render(
            "Elige una contraseña nueva para tu cuenta.",
            $"Hola {userName},",
            [
                "Para elegir una contraseña nueva en SITU abre este enlace.",
                $"El enlace vence en {minutes} minutos y solo se puede usar una vez. Si no pediste este cambio, ignora este mensaje.",
            ],
            new EmailButton("Elegir contraseña", resetUrl));

        return ("Restablece tu contraseña en SITU", body);
    }

    public static (string Subject, string Body) Changed(string userName)
    {
        var body = BrandedEmail.Render(
            "La contraseña de tu cuenta cambió.",
            $"Hola {userName},",
            [
                "La contraseña de tu cuenta se restableció y cerramos las sesiones abiertas. Si no fuiste tú, pide otro enlace de recuperación en cuanto puedas.",
            ]);

        return ("Tu contraseña de SITU cambió", body);
    }
}
