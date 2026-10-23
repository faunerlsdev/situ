using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ListMyDevices;
using Situ.Application.Features.Identity.RevokeDevice;
using Situ.Application.Features.Identity.SignOut;
using Situ.Application.Features.Identity.SignOutAllDevices;

namespace Situ.Api.Endpoints.Identity;

public sealed class SessionEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/sign-out", SignOut)
            .WithName("SignOut")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AllowAnonymous();

        group.MapPost("/auth/sign-out/all", SignOutAll)
            .WithName("SignOutAllDevices")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/auth/devices", ListDevices)
            .WithName("ListMyDevices")
            .WithTags("Identity")
            .Produces<ListMyDevicesResponse>()
            .RequireRateLimiting(AuthRateLimit.Policy)
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapDelete("/auth/devices/{deviceId:guid}", Revoke)
            .WithName("RevokeDevice")
            .WithTags("Identity")
            .RequireRateLimiting(AuthRateLimit.Policy)
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> SignOut(
        SignOutRequest request,
        IHandler<SignOutRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> SignOutAll(
        IHandler<SignOutAllDevicesRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new SignOutAllDevicesRequest(), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ListDevices(
        IHandler<ListMyDevicesRequest, Result<ListMyDevicesResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListMyDevicesRequest(), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Revoke(
        Guid deviceId,
        IHandler<RevokeDeviceRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RevokeDeviceRequest(deviceId), cancellationToken);
        return result.ToHttpResult();
    }
}
