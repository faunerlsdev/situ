using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.RefreshSession;

namespace Situ.Api.Endpoints.Identity;

public sealed class RefreshSessionEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/refresh", Handle)
            .WithName("RefreshToken")
            .WithTags("Identity")
            .Produces<RefreshSessionResponse>()
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        RefreshSessionRequest request,
        HttpContext httpContext,
        IHandler<RefreshSessionRequest, Result<RefreshSessionResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            request with { IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() },
            cancellationToken);
        return result.ToHttpResult();
    }
}
