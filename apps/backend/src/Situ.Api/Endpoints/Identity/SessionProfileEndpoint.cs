using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.GetSessionProfile;

namespace Situ.Api.Endpoints.Identity;

public sealed class SessionProfileEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me", Handle)
            .WithName("GetSessionProfile")
            .WithTags("Identity")
            .WithSummary("Perfil de la sesión. El correo va enmascarado. No incluye fecha de nacimiento ni edad.")
            .Produces<GetSessionProfileResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> Handle(
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<GetSessionProfileRequest, Result<GetSessionProfileResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(new GetSessionProfileRequest(userId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(result.Value);
    }
}
