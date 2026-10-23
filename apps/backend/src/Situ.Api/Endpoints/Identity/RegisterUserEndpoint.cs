using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.RegisterUser;

namespace Situ.Api.Endpoints.Identity;

public sealed class RegisterUserEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/register", Handle)
            .WithName("RegisterUser")
            .WithTags("Identity")
            .Produces<RegisterUserResponse>()
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        RegisterUserRequest request,
        HttpContext httpContext,
        IHandler<RegisterUserRequest, Result<RegisterUserResponse>> handler,
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
