using Situ.Application.Common.Mail;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity;

internal static class EmailConfirmationMail
{
    public static (string Subject, string Body) Compose(string userName, string confirmationUrl)
    {
        var hours = (int)EmailConfirmationPolicy.TokenLifetime.TotalHours;
        var body = BrandedEmail.Render(
            "Confirma tu correo para activar la cuenta.",
            $"Hola {userName},",
            [
                "Para activar tu cuenta en SITU abre este enlace.",
                $"El enlace vence en {hours} horas y solo se puede usar una vez. Si no creaste esta cuenta, ignora este mensaje.",
            ],
            new EmailButton("Confirmar correo", confirmationUrl));

        return ("Confirma tu correo en SITU", body);
    }
}
