using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity;

internal static class EmailConfirmationMail
{
    public static (string Subject, string Body) Compose(string userName, string confirmationUrl)
    {
        var hours = (int)EmailConfirmationPolicy.TokenLifetime.TotalHours;
        var body = $"""
            Hola {userName},

            Para activar tu cuenta en SITU abre este enlace:

            {confirmationUrl}

            El enlace vence en {hours} horas y solo se puede usar una vez. Si no creaste esta cuenta, ignora este mensaje.

            SITU
            """;

        return ("Confirma tu correo en SITU", body);
    }
}
