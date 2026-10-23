using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.SignIn;

namespace Situ.Api.Endpoints.Identity;

public sealed class SignInEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/login", Handle)
            .WithName("SignIn")
            .WithTags("Identity")
            .Produces<SignInResponse>()
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        SignInRequest request,
        HttpContext httpContext,
        IHandler<SignInRequest, Result<SignInResponse>> handler,
        CancellationToken cancellationToken)
    {
        var enriched = request with
        {
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
        };
        var result = await handler.Handle(enriched, cancellationToken);
        return result.ToHttpResult();
    }
}
