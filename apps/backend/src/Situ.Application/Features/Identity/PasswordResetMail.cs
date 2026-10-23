using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity;

internal static class PasswordResetMail
{
    public static (string Subject, string Body) Link(string userName, string resetUrl)
    {
        var minutes = (int)PasswordResetPolicy.TokenLifetime.TotalMinutes;
        var body = $"""
            Hola {userName},

            Para elegir una contraseña nueva en SITU abre este enlace:

            {resetUrl}

            El enlace vence en {minutes} minutos y solo se puede usar una vez. Si no pediste este cambio, ignora este mensaje.

            SITU
            """;

        return ("Restablece tu contraseña en SITU", body);
    }

    public static (string Subject, string Body) Changed(string userName)
    {
        var body = $"""
            Hola {userName},

            La contraseña de tu cuenta se restableció y cerramos las sesiones abiertas. Si no fuiste tú, pide otro enlace de recuperación en cuanto puedas.

            SITU
            """;

        return ("Tu contraseña de SITU cambió", body);
    }
}
