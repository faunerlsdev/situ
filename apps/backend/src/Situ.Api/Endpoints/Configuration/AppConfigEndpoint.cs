using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Configuration.GetAppConfig;

namespace Situ.Api.Endpoints.Configuration;

public sealed class AppConfigEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/app-config", Handle)
            .WithName("GetAppConfig")
            .WithTags("Configuration")
            .WithSummary("Textos del panel, límites de media y versiones, leídos de SystemSettings.")
            .Produces<GetAppConfigResponse>()
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        HttpContext httpContext,
        IHandler<GetAppConfigRequest, Result<GetAppConfigResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetAppConfigRequest(), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        var etag = "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result.Value)))) + "\"";
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "public, max-age=60";
        if (string.Equals(httpContext.Request.Headers.IfNoneMatch.ToString(), etag, StringComparison.Ordinal))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(result.Value);
    }
}
