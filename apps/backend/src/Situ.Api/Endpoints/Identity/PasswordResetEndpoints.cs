using System.Net;
using System.Text.Json;
using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ForgotPassword;
using Situ.Application.Features.Identity.ResetPassword;
using Situ.Application.Features.Identity.ValidateResetToken;

namespace Situ.Api.Endpoints.Identity;

public sealed class PasswordResetEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/password-reset", Forgot)
            .WithName("ForgotPassword")
            .WithTags("Identity")
            .Produces(StatusCodes.Status202Accepted)
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapGet("/auth/password-reset/validate", Validate)
            .WithName("ValidateResetToken")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapGet("/auth/password-reset", OpenLink)
            .WithName("OpenPasswordResetLink")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/auth/password-reset/confirm", Reset)
            .WithName("ResetPassword")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();
    }

    private static async Task<IResult> Forgot(
        ForgotPasswordRequest request,
        IHandler<ForgotPasswordRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.IsSuccess ? Results.Accepted() : result.ToHttpResult();
    }

    private static async Task<IResult> Validate(
        string token,
        IHandler<ValidateResetTokenRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ValidateResetTokenRequest(token), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> OpenLink(
        string? token,
        IHandler<ValidateResetTokenRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Html(false, "El enlace para restablecer la contraseña no es válido.", null);
        }

        var result = await handler.Handle(new ValidateResetTokenRequest(token), cancellationToken);
        if (result.IsFailure)
        {
            return Html(false, result.Error!.Message, null);
        }

        return Html(true, null, token);
    }

    private static async Task<IResult> Reset(
        ResetPasswordRequest request,
        IHandler<ResetPasswordRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static IResult Html(bool valid, string? message, string? token)
    {
        var body = valid ? Form(token!) : $"""
            <h1>Enlace no disponible</h1>
            <p>{WebUtility.HtmlEncode(message)}</p>
            """;

        return Results.Content(
            $"""
            <!doctype html>
            <html lang="es">
            <meta charset="utf-8">
            <title>Restablecer contraseña · SITU</title>
            <body style="font-family: sans-serif; padding: 2rem;">
            {body}
            </body>
            </html>
            """,
            "text/html; charset=utf-8");
    }

    private static string Form(string token)
    {
        var script = """
            <script>
            const token = TOKEN_JSON;
            const form = document.getElementById('reset');
            const status = document.getElementById('status');
            form.addEventListener('submit', async (event) => {
              event.preventDefault();
              const password = new FormData(form).get('password');
              const response = await fetch('/api/v1/auth/password-reset/confirm', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({ token, password })
              });
              if (response.ok) {
                status.textContent = 'Tu contraseña quedó restablecida. Ya puedes ingresar.';
                form.remove();
                return;
              }
              const problem = await response.json().catch(() => ({}));
              status.textContent = problem.detail || 'No pudimos restablecer la contraseña.';
            });
            </script>
            """;

        return $"""
            <h1>Nueva contraseña</h1>
            <form id="reset">
              <label>Contraseña <input name="password" type="password" required minlength="8" autocomplete="new-password"></label>
              <button type="submit">Guardar</button>
            </form>
            <p id="status"></p>
            {script.Replace("TOKEN_JSON", JsonSerializer.Serialize(token), StringComparison.Ordinal)}
            """;
    }
}
