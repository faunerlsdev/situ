using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ResendConfirmationEmail;
using Situ.Application.Features.Identity.ResendMyConfirmationEmail;

namespace Situ.Api.Endpoints.Identity;

public sealed class ResendConfirmationEmailEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/confirm-email/resend", Handle)
            .WithName("ResendConfirmationEmail")
            .WithTags("Identity")
            .AllowAnonymous();

        group.MapPost("/auth/confirm-email/resend-self", HandleSelf)
            .WithName("ResendMyConfirmationEmail")
            .WithTags("Identity")
            .WithSummary("Reenvía la confirmación a la cuenta de la sesión, sin pedir el correo al cliente.")
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> HandleSelf(
        ICurrentUser currentUser,
        IHandler<ResendMyConfirmationEmailRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(new ResendMyConfirmationEmailRequest(userId), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Handle(
        ResendConfirmationEmailRequest request,
        IHandler<ResendConfirmationEmailRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }
}
