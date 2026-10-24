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
        string? token,
        IHandler<ConfirmEmailRequest, Result<ConfirmEmailResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthLinkPage.Show(
                "No pudimos confirmar el correo",
                "El enlace de confirmación no es válido.",
                success: false,
                StatusCodes.Status404NotFound);
        }

        var result = await handler.Handle(new ConfirmEmailRequest(token), cancellationToken);
        if (result.IsFailure)
        {
            return AuthLinkPage.Show(
                "No pudimos confirmar el correo",
                result.Error!.Message,
                success: false,
                StatusCode(result.Error.Type));
        }

        return AuthLinkPage.Show(
            "Correo confirmado",
            "Ya puedes ingresar a SITU. Si eres menor de edad, la cuenta sigue pendiente de la autorización de tu acudiente.",
            success: true,
            StatusCodes.Status200OK);
    }

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}
