using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.AcknowledgeMinorNotice;
using Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;
using Situ.Application.Features.Guardianship.CancelGuardianRegistration;
using Situ.Application.Features.Guardianship.ChangeGuardianEmail;
using Situ.Application.Features.Guardianship.DeclineGuardianConsent;
using Situ.Application.Features.Guardianship.GetGuardianConsentForReview;
using Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;
using Situ.Application.Features.Guardianship.OptOutGuardianEmail;
using Situ.Application.Features.Guardianship.RequestGuardianConsent;
using Situ.Application.Features.Guardianship.ResendGuardianConsentEmail;
using Situ.Application.Features.Guardianship.ResendGuardianEmailCode;
using Situ.Application.Features.Guardianship.RevokeGuardianConsent;
using Situ.Application.Features.Guardianship.SubmitGuardianData;
using Situ.Application.Features.Guardianship.VerifyGuardianEmail;
using Situ.Application.Features.Guardianship.VerifyGuardianEmailCode;

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

        group.MapGet("/guardianship/mine", Mine)
            .WithName("GetMyGuardianConsentStatus")
            .WithTags("Guardianship")
            .Produces<GuardianConsentStatusResponse>()
            .RequireAuthorization(AuthorizationPolicies.GuardianConsent);

        group.MapPost("/guardianship/mine", RequestMine)
            .WithName("RequestGuardianConsent")
            .WithTags("Guardianship")
            .Produces<GuardianConsentStatusResponse>()
            .RequireAuthorization(AuthorizationPolicies.GuardianConsent);

        group.MapPost("/guardianship/mine/resend", ResendMine)
            .WithName("ResendGuardianConsentEmail")
            .WithTags("Guardianship")
            .RequireAuthorization(AuthorizationPolicies.GuardianConsent);

        group.MapPost("/guardianship/mine/email", ChangeMine)
            .WithName("ChangeGuardianEmail")
            .WithTags("Guardianship")
            .RequireAuthorization(AuthorizationPolicies.GuardianConsent);

        group.MapPost("/guardianship/mine/notice", AcknowledgeMine)
            .WithName("AcknowledgeMinorNotice")
            .WithTags("Guardianship")
            .RequireAuthorization(AuthorizationPolicies.GuardianConsent);

        group.MapDelete("/guardianship/mine", CancelMine)
            .WithName("CancelGuardianRegistration")
            .WithTags("Guardianship")
            .RequireAuthorization(AuthorizationPolicies.GuardianConsent);

        group.MapGet("/guardianship/consent/review", Review)
            .WithName("GetGuardianConsentForReview")
            .WithTags("Guardianship")
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/verify-code", VerifyCode)
            .WithName("VerifyGuardianEmailCode")
            .WithTags("Guardianship")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/resend-code", ResendCode)
            .WithName("ResendGuardianEmailCode")
            .WithTags("Guardianship")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/opt-out", OptOut)
            .WithName("OptOutGuardianEmail")
            .WithTags("Guardianship")
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/revoke", Revoke)
            .WithName("RevokeGuardianConsent")
            .WithTags("Guardianship")
            .AllowAnonymous();

        group.MapPost("/guardianship/consent/permissions", UpdatePermissions)
            .WithName("UpdateGuardianPermissions")
            .WithTags("Guardianship")
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
            return AuthLinkPage.Show(
                "No pudimos verificar el correo",
                "El enlace de verificación no es válido.",
                success: false,
                StatusCodes.Status404NotFound);
        }

        var result = await handler.Handle(new VerifyGuardianEmailRequest(token), cancellationToken);
        return result.IsSuccess
            ? AuthLinkPage.Show(
                "Correo verificado",
                "Ya puedes volver a la página y autorizar la cuenta del menor.",
                success: true,
                StatusCodes.Status200OK)
            : AuthLinkPage.Show(
                "No pudimos verificar el correo",
                result.Error!.Message,
                success: false,
                StatusCodes.Status400BadRequest);
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

    private static async Task<IResult> Mine(
        IHandler<GetMyGuardianConsentStatusRequest, Result<GuardianConsentStatusResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetMyGuardianConsentStatusRequest(), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RequestMine(
        RequestGuardianConsentRequest request,
        HttpContext httpContext,
        IHandler<RequestGuardianConsentRequest, Result<GuardianConsentStatusResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request with { IpAddress = Ip(httpContext) }, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ResendMine(
        HttpContext httpContext,
        IHandler<ResendGuardianConsentEmailRequest, Result<GuardianConsentStatusResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ResendGuardianConsentEmailRequest(Ip(httpContext)), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ChangeMine(
        ChangeGuardianEmailRequest request,
        HttpContext httpContext,
        IHandler<ChangeGuardianEmailRequest, Result<GuardianConsentStatusResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request with { IpAddress = Ip(httpContext) }, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AcknowledgeMine(
        HttpContext httpContext,
        IHandler<AcknowledgeMinorNoticeRequest, Result<GuardianConsentStatusResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new AcknowledgeMinorNoticeRequest(Ip(httpContext), httpContext.Request.Headers.UserAgent.ToString()),
            cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CancelMine(
        IHandler<CancelGuardianRegistrationRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CancelGuardianRegistrationRequest(), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Review(
        string? token,
        IHandler<GetGuardianConsentForReviewRequest, Result<GuardianConsentReviewResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetGuardianConsentForReviewRequest(token), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> VerifyCode(
        VerifyGuardianEmailCodeRequest request,
        IHandler<VerifyGuardianEmailCodeRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ResendCode(
        ResendGuardianEmailCodeRequest request,
        IHandler<ResendGuardianEmailCodeRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> OptOut(
        OptOutGuardianEmailRequest request,
        IHandler<OptOutGuardianEmailRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Revoke(
        RevokeGuardianConsentRequest request,
        IHandler<RevokeGuardianConsentRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdatePermissions(
        UpdateGuardianPermissionsRequest request,
        IHandler<UpdateGuardianPermissionsRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static string? Ip(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();
}
