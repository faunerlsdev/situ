using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ResendConfirmationEmail;

namespace Situ.Api.Endpoints.Identity;

public sealed class ResendConfirmationEmailEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/confirm-email/resend", Handle)
            .WithName("ResendConfirmationEmail")
            .WithTags("Identity")
            .AllowAnonymous();
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
