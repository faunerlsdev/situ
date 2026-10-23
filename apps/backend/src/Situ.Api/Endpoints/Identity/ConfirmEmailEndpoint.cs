using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ConfirmEmail;

namespace Situ.Api.Endpoints.Identity;

public sealed class ConfirmEmailEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/confirm-email", HandlePost)
            .WithName("ConfirmEmail")
            .WithTags("Identity")
            .Produces<ConfirmEmailResponse>()
            .AllowAnonymous();

        group.MapGet("/auth/confirm-email", HandleGet)
            .WithName("ConfirmEmailFromLink")
            .WithTags("Identity")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandlePost(
        ConfirmEmailRequest request,
        IHandler<ConfirmEmailRequest, Result<ConfirmEmailResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> HandleGet(
        string token,
        IHandler<ConfirmEmailRequest, Result<ConfirmEmailResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ConfirmEmailRequest(token), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        return Results.Content(
            """
            <!doctype html>
            <html lang="es">
            <meta charset="utf-8">
            <title>Correo confirmado · SITU</title>
            <body style="font-family: sans-serif; padding: 2rem;">
              <h1>Correo confirmado</h1>
              <p>Ya puedes ingresar a SITU. Si eres menor de edad, la cuenta sigue pendiente de la autorización de tu acudiente.</p>
            </body>
            </html>
            """,
            "text/html; charset=utf-8");
    }
}
