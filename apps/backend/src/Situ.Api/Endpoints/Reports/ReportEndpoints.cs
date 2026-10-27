using Situ.Api.Auth;
using Situ.Api.Configuration;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Application.Features.Reports.ConfirmReport;
using Situ.Application.Features.Reports.CreateReport;
using Situ.Application.Features.Reports.DeleteReport;
using Situ.Application.Features.Reports.GetReport;
using Situ.Application.Features.Reports.GetReportCatalog;
using Situ.Application.Features.Reports.GetReportBatch;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Application.Features.Reports.GetPublicReportMap;
using Situ.Application.Features.Reports.GetReportShare;
using Situ.Application.Features.Reports.ListMyReports;
using Situ.Application.Features.Reports.OpenReportMedia;
using Situ.Application.Features.Reports.ManageReportCategories;
using Situ.Application.Features.Reports.RemoveReport;
using Situ.Application.Features.Reports.UpdateReport;
using Situ.Application.Features.Reports.UploadReportMedia;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Api.Endpoints.Reports;

public sealed class ReportEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/report-categories", Catalog)
            .WithName("GetReportCatalog")
            .WithTags("Reports")
            .WithSummary("Catálogo de categorías resuelto para quien consulta.")
            .WithDescription("Aplica overrides, idioma y mayoría de edad. Responde 304 si el ETag no cambió.")
            .Produces<GetReportCatalogResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .AllowAnonymous()
            .RequireRateLimiting("catalog-read");

        group.MapPost("/reports/media", Upload)
            .WithName("UploadReportMedia")
            .WithTags("Reports")
            .WithSummary("Sube una imagen o un video. Si pasa la validación, el mediaId queda Ready para asociarlo a un reporte.")
            .Produces<UploadReportMediaResponse>()
            .DisableAntiforgery()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("reports-write");

        group.MapPost("/reports", Create)
            .WithName("CreateReport")
            .WithTags("Reports")
            .WithSummary("Publica un reporte en el mapa.")
            .Produces<CreateReportResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("reports-write");

        group.MapGet("/reports/batch", Batch)
            .WithName("GetReportBatch")
            .WithTags("Reports")
            .WithSummary("Contenido de reportes por id, con la visibilidad de quien consulta.")
            .WithDescription("El mapa en tiempo real pide aquí el contenido. Máximo 50 ids. Los que esta cuenta no puede ver no vienen en la respuesta.")
            .Produces<GetReportBatchResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/reports/map", MapReports)
            .WithName("GetReportMap")
            .WithTags("Reports")
            .WithSummary("Reportes de la zona visible, agrupados según el zoom.")
            .Produces<GetReportMapResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/reports/mine", Mine)
            .WithName("ListMyReports")
            .WithTags("Reports")
            .WithSummary("Reportes de quien consulta, incluidos los anónimos.")
            .Produces<ListMyReportsResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/reports/{id:guid}", Detail)
            .WithName("GetReport")
            .WithTags("Reports")
            .WithSummary("Detalle de un reporte, con multimedia y comentarios resumidos.")
            .Produces<GetReportResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/reports/{id:guid}/share", Share)
            .WithName("GetReportShare")
            .WithTags("Reports")
            .WithSummary("Tarjeta pública para compartir: categoría y zona aproximada, sin texto ni autor.")
            .WithDescription("Los reportes de menores o que no son públicos responden sin datos y no deben indexarse.")
            .Produces<GetReportShareResponse>()
            .AllowAnonymous()
            .RequireRateLimiting("share-public");

        group.MapGet("/public/reports/map", PublicMap)
            .WithName("GetPublicReportMap")
            .WithTags("Reports")
            .WithSummary("Mapa limitado para quien no tiene cuenta: pines públicos de adultos, últimas 24 horas.")
            .WithDescription("Sin autor, texto, medios ni conteos. La ubicación se redondea a 3 decimales (~111 m). Un token no cambia la respuesta.")
            .Produces<GetPublicReportMapResponse>()
            .AllowAnonymous()
            .RequireRateLimiting("public-map");

        group.MapPatch("/reports/{id:guid}", Update)
            .WithName("UpdateReport")
            .WithTags("Reports")
            .WithSummary("El autor cambia la descripción mientras el reporte está activo.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapDelete("/reports/{id:guid}", Delete)
            .WithName("DeleteReport")
            .WithTags("Reports")
            .WithSummary("El autor retira su reporte. Los archivos se borran según la retención.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPut("/reports/{id:guid}/confirmation", Confirm)
            .WithName("ConfirmReport")
            .WithTags("Reports")
            .WithSummary("Un voto de sigue ahí o ya no está. Cambiarlo reemplaza el anterior.")
            .Produces<ConfirmReportResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("reports-write");

        group.MapGet("/reports/media/{id:guid}/content", Content)
            .WithName("GetReportMediaContent")
            .WithTags("Reports")
            .WithSummary("Descarga un archivo con sesión o con una URL firmada. No expone la ruta del disco.")
            .AllowAnonymous();

        group.MapPost("/admin/report-categories", CreateCategory)
            .WithName("CreateReportCategory")
            .WithTags("ReportCategories")
            .WithSummary("Crea una categoría. Requiere permiso categories.manage.")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        group.MapPut("/admin/report-categories/{id:guid}", UpdateCategory)
            .WithName("UpdateReportCategory")
            .WithTags("ReportCategories")
            .WithSummary("Edita una categoría.")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        group.MapPost("/admin/report-categories/{id:guid}/active", SetActive)
            .WithName("SetReportCategoryActive")
            .WithTags("ReportCategories")
            .WithSummary("Activa o desactiva una categoría.")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        group.MapPut("/admin/report-categories/order", Reorder)
            .WithName("ReorderReportCategories")
            .WithTags("ReportCategories")
            .WithSummary("Reordena el catálogo.")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        group.MapPut("/admin/report-categories/{id:guid}/overrides", Override)
            .WithName("UpsertReportCategoryOverride")
            .WithTags("ReportCategories")
            .WithSummary("Habilita, deshabilita o cambia el TTL de una categoría en un país o ciudad.")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        group.MapPut("/admin/report-categories/{id:guid}/translations", Translation)
            .WithName("UpsertReportCategoryTranslation")
            .WithTags("ReportCategories")
            .WithSummary("Crea o actualiza la traducción de una categoría.")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        group.MapPost("/moderation/reports/{id:guid}/remove", Remove)
            .WithName("RemoveReport")
            .WithTags("Moderation")
            .WithSummary("El personal retira un reporte. El motivo queda en la auditoría.")
            .RequireAuthorization(AuthorizationPolicies.Staff);
    }

    private static async Task<IResult> Catalog(
        HttpContext httpContext,
        ICurrentUser currentUser,
        PublicBaseUrlOptions assets,
        IHandler<GetReportCatalogRequest, Result<GetReportCatalogResponse>> handler,
        CancellationToken cancellationToken)
    {
        var scope = httpContext.User.FindFirst("scope")?.Value;
        var userId = scope == AuthorizationPolicies.GuardianConsentScope ? null : currentUser.UserId;
        var result = await handler.Handle(
            new GetReportCatalogRequest(userId, httpContext.Request.Headers.AcceptLanguage.ToString()),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        var catalog = result.Value!;
        var etag = $"\"{catalog.CatalogVersion}:{catalog.Locale}:{catalog.AdultCatalog}:{catalog.CityId}\"";
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "private, max-age=300";
        if (string.Equals(httpContext.Request.Headers.IfNoneMatch.ToString(), etag, StringComparison.Ordinal))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(catalog with
        {
            Categories = catalog.Categories
                .Select(category => category with
                {
                    ButtonIconUrl = PublicAssetUrls.Absolute(assets.Url, category.ButtonIconUrl),
                    MapIconUrl = PublicAssetUrls.Absolute(assets.Url, category.MapIconUrl),
                })
                .ToArray(),
        });
    }

    private static async Task<IResult> Upload(
        IFormFile file,
        ICurrentUser currentUser,
        IHandler<UploadReportMediaRequest, Result<UploadReportMediaResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || file is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Adjunta un archivo.", extensions: new Dictionary<string, object?> { ["code"] = "report.media_required" });
        }

        await using var stream = file.OpenReadStream();
        var result = await handler.Handle(new UploadReportMediaRequest(userId, stream, file.Length), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(
        CreateReportBody body,
        ICurrentUser currentUser,
        IHandler<CreateReportRequest, Result<CreateReportResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (!Enum.TryParse<ContentVisibility>(body.Visibility, true, out var visibility))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "La visibilidad debe ser Public o FriendsOnly.", extensions: new Dictionary<string, object?> { ["code"] = "report.invalid_visibility" });
        }

        var result = await handler.Handle(
            new CreateReportRequest(userId, body.CategoryId, body.Description, body.Latitude, body.Longitude, visibility, body.IsAnonymous, body.MediaIds ?? []),
            cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> PublicMap(
        double minLat,
        double minLon,
        double maxLat,
        double maxLon,
        IHandler<GetPublicReportMapRequest, Result<GetPublicReportMapResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetPublicReportMapRequest(minLat, minLon, maxLat, maxLon), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Share(
        Guid id,
        IHandler<GetReportShareRequest, Result<GetReportShareResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetReportShareRequest(id), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> MapReports(
        double minLat,
        double minLon,
        double maxLat,
        double maxLon,
        int zoom,
        string? categories,
        string? since,
        string? type,
        double? lat,
        double? lon,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<GetReportMapRequest, Result<GetReportMapResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var list = string.IsNullOrWhiteSpace(categories)
            ? []
            : categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await handler.Handle(
            new GetReportMapRequest(userId, minLat, minLon, maxLat, maxLon, zoom, list, since, type, lat, lon),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> Batch(
        string? ids,
        double? lat,
        double? lon,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<GetReportBatchRequest, Result<GetReportBatchResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var parsed = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(ids))
        {
            foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var id))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["ids"] = ["Cada id tiene que ser un identificador de reporte."],
                    });
                }

                parsed.Add(id);
            }
        }

        var result = await handler.Handle(new GetReportBatchRequest(userId, parsed, lat, lon), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> Mine(
        string? cursor,
        int? limit,
        ICurrentUser currentUser,
        IHandler<ListMyReportsRequest, Result<ListMyReportsResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(new ListMyReportsRequest(userId, cursor, limit ?? 20), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Detail(
        Guid id,
        ICurrentUser currentUser,
        IHandler<GetReportRequest, Result<GetReportResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetReportRequest(currentUser.UserId, id), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateReportBody body,
        ICurrentUser currentUser,
        IHandler<UpdateReportRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(new UpdateReportRequest(userId, id, body.Description), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(
        Guid id,
        ICurrentUser currentUser,
        IHandler<DeleteReportRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(new DeleteReportRequest(userId, id), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Confirm(
        Guid id,
        ConfirmReportBody body,
        ICurrentUser currentUser,
        IHandler<ConfirmReportRequest, Result<ConfirmReportResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (!Enum.TryParse<ConfirmationType>(body.Type, true, out var type))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "El voto debe ser StillThere o GoneNow.", extensions: new Dictionary<string, object?> { ["code"] = "report.invalid_confirmation" });
        }

        var result = await handler.Handle(new ConfirmReportRequest(userId, id, type, body.Latitude, body.Longitude), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Content(
        Guid id,
        long? exp,
        string? sig,
        string? kind,
        ICurrentUser currentUser,
        IHandler<OpenReportMediaRequest, Result<OpenReportMediaResponse>> handler,
        IFileStorage storage,
        IMediaUrlSigner urls,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var signed = exp is { } expires && urls.IsValid(id, expires, sig ?? "", clock.UtcNow);
        if (!signed && currentUser.UserId is null)
        {
            return Results.Unauthorized();
        }

        var opened = await handler.Handle(new OpenReportMediaRequest(currentUser.UserId, id, signed, kind == "thumb"), cancellationToken);
        if (opened.IsFailure)
        {
            return opened.ToHttpResult();
        }

        var stream = await storage.OpenReadAsync(opened.Value!.StorageKey, cancellationToken);
        return stream is null ? Results.NotFound() : Results.File(stream, opened.Value.ContentType);
    }

    private static async Task<IResult> CreateCategory(
        CreateReportCategoryRequest body,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<CreateReportCategoryRequest, Result<Guid>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = currentUser.UserId ?? Guid.Empty, IpAddress = Ip(httpContext) }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateCategory(
        Guid id,
        UpdateReportCategoryRequest body,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<UpdateReportCategoryRequest, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = currentUser.UserId ?? Guid.Empty, IpAddress = Ip(httpContext), CategoryId = id }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> SetActive(
        Guid id,
        ActiveBody body,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<SetReportCategoryActiveRequest, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new SetReportCategoryActiveRequest(currentUser.UserId ?? Guid.Empty, Ip(httpContext), id, body.IsActive), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Reorder(
        ReorderBody body,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<ReorderReportCategoriesRequest, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ReorderReportCategoriesRequest(currentUser.UserId ?? Guid.Empty, Ip(httpContext), body.CategoryIds), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Override(
        Guid id,
        UpsertReportCategoryOverrideRequest body,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<UpsertReportCategoryOverrideRequest, Result<Guid>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = currentUser.UserId ?? Guid.Empty, IpAddress = Ip(httpContext), CategoryId = id }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> Translation(
        Guid id,
        UpsertReportCategoryTranslationRequest body,
        HttpContext httpContext,
        ICurrentUser currentUser,
        IHandler<UpsertReportCategoryTranslationRequest, Result<Guid>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = currentUser.UserId ?? Guid.Empty, IpAddress = Ip(httpContext), CategoryId = id }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> Remove(
        Guid id,
        RemoveBody body,
        ICurrentUser currentUser,
        IHandler<RemoveReportRequest, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new RemoveReportRequest(currentUser.UserId ?? Guid.Empty, id, body.Reason), cancellationToken)).ToHttpResult();

    private static string? Ip(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();
}

public sealed record CreateReportBody(Guid CategoryId, string Description, double Latitude, double Longitude, string Visibility, bool IsAnonymous, IReadOnlyList<Guid>? MediaIds);

public sealed record UpdateReportBody(string Description);

public sealed record ConfirmReportBody(string Type, double Latitude, double Longitude);

public sealed record ActiveBody(bool IsActive);

public sealed record ReorderBody(IReadOnlyList<Guid> CategoryIds);

public sealed record RemoveBody(string Reason);
