using Microsoft.Extensions.Options;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Live;

namespace Situ.Api.Live;

public static class LiveIngressEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/internal/live/authorize", Authorize).AllowAnonymous();
        app.MapGet("/internal/live/hls", Hls).AllowAnonymous();
        app.MapGet("/internal/live/hls/{*extra}", Hls).AllowAnonymous().Add(static builder =>
        {
            if (builder is RouteEndpointBuilder route)
            {
                route.Order = 1;
            }
        });
    }

    private static async Task<IResult> Authorize(
        HttpContext http,
        AuthorizeBody? body,
        IOptions<LiveStreamOptions> options,
        IHandler<AuthorizeLiveRequest, Situ.Application.Common.Results.Result> handler,
        ILoggerFactory logs,
        CancellationToken cancellationToken)
    {
        var secret = http.Request.Headers["X-Situ-Live-Secret"].ToString();
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = http.Request.Query["secret"].ToString();
        }

        var result = await handler.Handle(
            new AuthorizeLiveRequest(
                LiveIngress.IsLoopback(http.Connection.RemoteIpAddress),
                secret,
                body?.Action ?? "",
                body?.Path ?? "",
                body?.Password,
                body?.Query),
            cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        var path = body?.Path ?? "";
        logs.CreateLogger("Situ.Live.Authorize").LogInformation(
            "Live authorize rejected ({Reason}). PathKnown {PathKnown}.",
            result.Error?.Code ?? "live.unauthorized",
            IsOpaquePath(path));
        return Results.Unauthorized();
    }

    private static async Task<IResult> Hls(
        HttpContext http,
        IHandler<CheckHlsTokenRequest, Result<CheckHlsTokenResponse>> handler,
        ILoggerFactory logs,
        CancellationToken cancellationToken)
    {
        var uri = http.Request.Headers["X-Original-Uri"].ToString();
        if (string.IsNullOrWhiteSpace(uri))
        {
            uri = http.Request.Path + http.Request.QueryString;
        }

        var result = await handler.Handle(
            new CheckHlsTokenRequest(
                LiveIngress.IsLoopback(http.Connection.RemoteIpAddress),
                uri,
                http.Request.Headers["X-Live-Token"].ToString(),
                null),
            cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Empty;
        }

        var code = result.Error?.Code ?? "live.hls_forbidden";
        var path = LiveTokens.PathFromUri(uri);
        logs.CreateLogger("Situ.Live.Hls").LogInformation(
            "Live HLS rejected ({Reason}). PathKnown {PathKnown}.",
            code,
            IsOpaquePath(path));
        return result.Error?.Type == ErrorType.Forbidden
            ? Results.StatusCode(StatusCodes.Status403Forbidden)
            : Results.StatusCode(StatusCodes.Status401Unauthorized);
    }

    private static bool IsOpaquePath(string? path)
    {
        if (path is not { Length: 32 })
        {
            return false;
        }

        foreach (var letter in path)
        {
            if (letter is not (>= 'a' and <= 'f' or >= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record AuthorizeBody(
        string? Action,
        string? Path,
        string? Password,
        string? Query,
        string? Ip,
        string? User,
        string? Token,
        string? Protocol,
        string? Id);
}
