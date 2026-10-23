using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;
using Situ.Application.Features.Guardianship.DeclineGuardianConsent;
using Situ.Application.Features.Guardianship.SubmitGuardianData;
using Situ.Application.Features.Guardianship.VerifyGuardianEmail;

namespace Situ.Api.Endpoints.Guardianship;

public sealed class GuardianConsentEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/guardianship/consent/data", Submit)
            .WithName("SubmitGuardianData")
            .WithTags("Guardianship")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/verify-email", Verify)
            .WithName("VerifyGuardianEmail")
            .WithTags("Guardianship")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapGet("/guardianship/consent/verify-email", VerifyFromLink)
            .WithName("VerifyGuardianEmailFromLink")
            .WithTags("Guardianship")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/authorize", Authorize)
            .WithName("AuthorizeGuardianConsent")
            .WithTags("Guardianship")
            .Produces<AuthorizeGuardianConsentResponse>()
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/decline", Decline)
            .WithName("DeclineGuardianConsent")
            .WithTags("Guardianship")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();
    }

    private static async Task<IResult> Submit(
        SubmitGuardianDataRequest request,
        IHandler<SubmitGuardianDataRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Verify(
        VerifyGuardianEmailRequest request,
        IHandler<VerifyGuardianEmailRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> VerifyFromLink(
        string? token,
        IHandler<VerifyGuardianEmailRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Page("El enlace de verificación no es válido.");
        }

        var result = await handler.Handle(new VerifyGuardianEmailRequest(token), cancellationToken);
        return Page(result.IsSuccess
            ? "Correo verificado. Ya puedes autorizar la cuenta del menor."
            : result.Error!.Message);
    }

    private static async Task<IResult> Authorize(
        AuthorizeGuardianConsentRequest request,
        HttpContext httpContext,
        IHandler<AuthorizeGuardianConsentRequest, Result<AuthorizeGuardianConsentResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            request with
            {
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            },
            cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Decline(
        DeclineGuardianConsentRequest request,
        IHandler<DeclineGuardianConsentRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static IResult Page(string message) =>
        Results.Content(
            $"""
            <!doctype html>
            <html lang="es">
            <meta charset="utf-8">
            <title>Autorización · SITU</title>
            <body style="font-family: sans-serif; padding: 2rem;">
              <h1>Autorización del acudiente</h1>
              <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
            </body>
            </html>
            """,
            "text/html; charset=utf-8");
}
