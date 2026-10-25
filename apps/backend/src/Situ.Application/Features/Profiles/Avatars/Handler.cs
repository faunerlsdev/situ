using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Administration;
using Situ.Domain.Guardianship;
using Situ.Domain.Profiles;

namespace Situ.Application.Features.Profiles.Avatars;

public sealed record ListAvatarGalleryRequest;

public sealed record AvatarGalleryItemView(Guid Id, string Name, string ImageUrl, int SortOrder);

public sealed record ListAvatarGalleryResponse(IReadOnlyList<AvatarGalleryItemView> Items);

public sealed class ListAvatarGalleryHandler(IApplicationDbContext db, IPublicLinks links)
    : IHandler<ListAvatarGalleryRequest, Result<ListAvatarGalleryResponse>>
{
    public async Task<Result<ListAvatarGalleryResponse>> Handle(
        ListAvatarGalleryRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await db.AvatarGalleryItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(item => new AvatarGalleryItemView(item.Id, item.Name, links.Absolute(item.ImageUrl), item.SortOrder))
            .ToArray();
        return Result<ListAvatarGalleryResponse>.Success(new ListAvatarGalleryResponse(items));
    }
}

public sealed record SetGalleryAvatarRequest(Guid UserId, Guid GalleryItemId);

public sealed class SetGalleryAvatarValidator : AbstractValidator<SetGalleryAvatarRequest>
{
    public SetGalleryAvatarValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.GalleryItemId).NotEmpty();
    }
}

public sealed class SetGalleryAvatarHandler(IApplicationDbContext db, IClock clock, IFileStorage storage)
    : IHandler<SetGalleryAvatarRequest, Result>
{
    public async Task<Result> Handle(SetGalleryAvatarRequest request, CancellationToken cancellationToken)
    {
        var item = await db.AvatarGalleryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.GalleryItemId && row.IsActive, cancellationToken);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("avatar.not_found", "Ese ícono ya no está disponible."));
        }

        var user = await db.Users.FirstOrDefaultAsync(row => row.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        var profile = await AvatarSupport.ProfileAsync(db, user, clock.UtcNow, cancellationToken);
        var previous = AvatarSupport.CustomKey(profile);
        profile.UseGalleryAvatar(item.Id, item.ImageUrl);
        profile.MarkUpdated(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await AvatarSupport.DeleteCustomAsync(storage, previous, cancellationToken);
        return Result.Success();
    }
}

public sealed record RemoveAvatarRequest(Guid UserId);

public sealed class RemoveAvatarHandler(IApplicationDbContext db, IClock clock, IFileStorage storage)
    : IHandler<RemoveAvatarRequest, Result>
{
    public async Task<Result> Handle(RemoveAvatarRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            return Result.Success();
        }

        var previous = AvatarSupport.CustomKey(profile);
        profile.UseDefaultAvatar();
        profile.MarkUpdated(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await AvatarSupport.DeleteCustomAsync(storage, previous, cancellationToken);
        return Result.Success();
    }
}

public sealed record UploadAvatarPhotoRequest(Guid UserId, byte[] Content);

public sealed class UploadAvatarPhotoHandler(
    IApplicationDbContext db,
    IClock clock,
    IFileStorage storage,
    IAvatarImageProcessor images)
    : IHandler<UploadAvatarPhotoRequest, Result>
{
    public async Task<Result> Handle(UploadAvatarPhotoRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        if (!MinorProtectionPolicy.CanUploadProfilePhoto(user.AgeOn(now)))
        {
            return Result.Failure(Error.Forbidden(
                "avatar.minor_photo_not_allowed",
                "Quien es menor de 18 años usa un ícono de la galería."));
        }

        var settings = await ProfileRules.SettingsAsync(db, cancellationToken);
        var maxBytes = Math.Max(1, ProfileRules.Int(settings, SystemSettingKeys.ProfileAvatarMaxBytes, 5 * 1024 * 1024));
        var processed = images.Process(request.Content, maxBytes);
        if (processed.IsFailure)
        {
            return Result.Failure(processed.Error!);
        }

        var profile = await AvatarSupport.ProfileAsync(db, user, now, cancellationToken);
        var previous = AvatarSupport.CustomKey(profile);
        var key = $"avatars/{user.Id:N}/{Guid.CreateVersion7():N}.jpg";
        await storage.SaveAsync(key, processed.Value!, cancellationToken);
        profile.UseCustomAvatar(key);
        profile.MarkUpdated(now);
        await db.SaveChangesAsync(cancellationToken);
        await AvatarSupport.DeleteCustomAsync(storage, previous, cancellationToken);
        return Result.Success();
    }
}

public sealed record OpenAvatarContentRequest(Guid UserId, long ExpiresUnix, string Signature);

public sealed record OpenAvatarContentResponse(string StorageKey);

public sealed class OpenAvatarContentHandler(IApplicationDbContext db, IClock clock, IMediaUrlSigner urls)
    : IHandler<OpenAvatarContentRequest, Result<OpenAvatarContentResponse>>
{
    public async Task<Result<OpenAvatarContentResponse>> Handle(
        OpenAvatarContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!urls.IsValid(request.UserId, request.ExpiresUnix, request.Signature, clock.UtcNow))
        {
            return Result<OpenAvatarContentResponse>.Failure(Error.NotFound("avatar.not_found", "No encontramos esa foto."));
        }

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        var key = profile is null ? null : AvatarSupport.CustomKey(profile);
        if (key is null)
        {
            return Result<OpenAvatarContentResponse>.Failure(Error.NotFound("avatar.not_found", "No encontramos esa foto."));
        }

        return Result<OpenAvatarContentResponse>.Success(new OpenAvatarContentResponse(key));
    }
}

internal static class AvatarSupport
{
    public static async Task<UserProfile> ProfileAsync(
        IApplicationDbContext db,
        Domain.Identity.User user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = UserProfile.CreateDefault(user.Id, user.GivenNames.Value);
        profile.MarkCreated(now);
        db.UserProfiles.Add(profile);
        return profile;
    }

    public static string? CustomKey(UserProfile profile) =>
        profile.AvatarKind == AvatarKind.Custom && profile.AvatarUrl is { } url && url.StartsWith("avatars/", StringComparison.Ordinal)
            ? url
            : null;

    public static async Task DeleteCustomAsync(IFileStorage storage, string? key, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            return;
        }

        await storage.DeleteAsync(key, cancellationToken);
    }
}
