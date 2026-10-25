using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ChangePassword;

namespace Situ.Api.Endpoints.Identity;

public sealed class ChangePasswordEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/password", Handle)
            .WithName("ChangePassword")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/password", Handle)
            .WithName("ChangeMyPassword")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> Handle(
        ChangePasswordRequest request,
        IHandler<ChangePasswordRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }
}
