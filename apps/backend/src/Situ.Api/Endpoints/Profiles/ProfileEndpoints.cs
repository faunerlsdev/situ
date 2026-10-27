using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles.Avatars;
using Situ.Application.Features.Profiles.CheckUserName;
using Situ.Application.Features.Identity.ChangePhone;
using Situ.Application.Features.Legal.AcknowledgeAnonymityNotice;
using Situ.Application.Features.Profiles.UpdateMyProfile;
using Situ.Application.Features.Retention.RequestPersonalDataExport;

namespace Situ.Api.Endpoints.Profiles;

public sealed class ProfileEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/me/phone", ChangePhone)
            .WithName("ChangePhone")
            .WithTags("Profile")
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/anonymity-notice", AcknowledgeNotice)
            .WithName("AcknowledgeAnonymityNotice")
            .WithTags("Profile")
            .Produces<AcknowledgeAnonymityNoticeResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/data-export", ExportData)
            .WithName("RequestPersonalDataExport")
            .WithTags("Profile")
            .Produces<RequestPersonalDataExportResponse>()
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPatch("/me/profile", Update)
            .WithName("UpdateMyProfile")
            .WithTags("Profile")
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/users/username-available", Available)
            .WithName("CheckUserName")
            .WithTags("Profile")
            .Produces<CheckUserNameResponse>()
            .RequireRateLimiting("username-lookup")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/avatars/gallery", Gallery)
            .WithName("ListAvatarGallery")
            .WithTags("Profile")
            .Produces<ListAvatarGalleryResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPut("/me/avatar", GalleryAvatar)
            .WithName("SetGalleryAvatar")
            .WithTags("Profile")
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapDelete("/me/avatar", RemoveAvatar)
            .WithName("RemoveAvatar")
            .WithTags("Profile")
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/avatar/photo", Photo)
            .WithName("UploadAvatarPhoto")
            .WithTags("Profile")
            .DisableAntiforgery()
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/avatars/{userId:guid}/content", Content)
            .WithName("OpenAvatarContent")
            .WithTags("Profile")
            .AllowAnonymous();
    }

    private static async Task<IResult> ChangePhone(
        ChangePhoneBody body,
        HttpContext http,
        ICurrentUser currentUser,
        IHandler<ChangePhoneRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new ChangePhoneRequest(userId, http.Connection.RemoteIpAddress?.ToString(), body.CurrentPassword, body.Phone), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> AcknowledgeNotice(
        HttpContext http,
        ICurrentUser currentUser,
        IHandler<AcknowledgeAnonymityNoticeRequest, Result<AcknowledgeAnonymityNoticeResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(
            new AcknowledgeAnonymityNoticeRequest(userId, http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString()),
            cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> ExportData(
        ICurrentUser currentUser,
        IHandler<RequestPersonalDataExportRequest, Result<RequestPersonalDataExportResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new RequestPersonalDataExportRequest(userId), cancellationToken)).ToHttpResult();
    }

    private sealed record ChangePhoneBody(string CurrentPassword, string Phone);

    private static async Task<IResult> Update(
        UpdateProfileBody body,
        ICurrentUser currentUser,
        IHandler<UpdateMyProfileRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(
            new UpdateMyProfileRequest(userId, body.UserName, body.DisplayName, body.Bio, body.HomeCityId, body.PreferAnonymousReports),
            cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Available(
        string? userName,
        ICurrentUser currentUser,
        IHandler<CheckUserNameRequest, Result<CheckUserNameResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.Handle(new CheckUserNameRequest(userId, userName), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Gallery(
        IHandler<ListAvatarGalleryRequest, Result<ListAvatarGalleryResponse>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListAvatarGalleryRequest(), cancellationToken)).ToHttpResult();

    private static async Task<IResult> GalleryAvatar(
        GalleryAvatarBody body,
        ICurrentUser currentUser,
        IHandler<SetGalleryAvatarRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SetGalleryAvatarRequest(userId, body.GalleryItemId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> RemoveAvatar(
        ICurrentUser currentUser,
        IHandler<RemoveAvatarRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new RemoveAvatarRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Photo(
        IFormFile? file,
        ICurrentUser currentUser,
        IHandler<UploadAvatarPhotoRequest, Result> handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || file is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Adjunta una foto.",
                extensions: new Dictionary<string, object?> { ["code"] = "avatar.empty" });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "La foto supera el tamaño permitido.",
                extensions: new Dictionary<string, object?> { ["code"] = "avatar.too_large" });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var result = await handler.Handle(new UploadAvatarPhotoRequest(userId, buffer.ToArray()), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Content(
        Guid userId,
        long? exp,
        string? sig,
        IHandler<OpenAvatarContentRequest, Result<OpenAvatarContentResponse>> handler,
        IFileStorage storage,
        CancellationToken cancellationToken)
    {
        var opened = await handler.Handle(new OpenAvatarContentRequest(userId, exp ?? 0, sig ?? ""), cancellationToken);
        if (opened.IsFailure)
        {
            return opened.ToHttpResult();
        }

        var stream = await storage.OpenReadAsync(opened.Value!.StorageKey, cancellationToken);
        return stream is null
            ? Results.NotFound()
            : Results.File(stream, "image/jpeg");
    }
}

public sealed record UpdateProfileBody(string? UserName, string? DisplayName, string? Bio, Guid? HomeCityId, bool? PreferAnonymousReports = null);

public sealed record GalleryAvatarBody(Guid GalleryItemId);
