using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Social;
using Situ.Application.Features.Social.GetPublicProfile;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Profiles;

namespace Situ.Application.Features.Profiles.ProfileVisibility;

public sealed record SetProfileVisibilityRequest(Guid UserId, bool Public, string? ConsentVersion, bool? ShowBio, bool? ShowReports);

public sealed record ProfileVisibilityView(
    bool Public,
    string? ConsentVersion,
    string RequiredConsentVersion,
    string ConsentText,
    bool ShowBio,
    bool ShowReports,
    PublicProfileView? StrangerPreview,
    PublicProfileView FriendPreview);

public sealed class SetProfileVisibilityValidator : AbstractValidator<SetProfileVisibilityRequest>
{
    public SetProfileVisibilityValidator()
    {
        RuleFor(request => request.ConsentVersion).MaximumLength(40);
    }
}

public sealed class SetProfileVisibilityHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<SetProfileVisibilityRequest, Result<ProfileVisibilityView>>
{
    public async Task<Result<ProfileVisibilityView>> Handle(SetProfileVisibilityRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<ProfileVisibilityView>.Failure(SocialErrors.NotAvailable());
        }

        var settings = await SocialRules.SettingsAsync(db, cancellationToken);
        var required = settings.GetValueOrDefault(SystemSettingKeys.ProfilePublicConsentVersion) ?? "2026-10-27";
        if (request.Public)
        {
            if (user.IsMinor(now))
            {
                return Result<ProfileVisibilityView>.Failure(SocialErrors.MinorPublic());
            }

            if (string.IsNullOrWhiteSpace(request.ConsentVersion) || !string.Equals(request.ConsentVersion.Trim(), required, StringComparison.Ordinal))
            {
                return Result<ProfileVisibilityView>.Failure(SocialErrors.ConsentRequired());
            }
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile is null)
        {
            profile = UserProfile.CreateDefault(user.Id, user.UserName.Value);
            profile.MarkCreated(now);
            db.UserProfiles.Add(profile);
        }

        if (request.Public)
        {
            try
            {
                profile.MakePublic(user.AgeOn(now), now, request.ConsentVersion!.Trim());
            }
            catch (DomainException exception) when (exception.Code == "profile.minor_cannot_be_public")
            {
                return Result<ProfileVisibilityView>.Failure(SocialErrors.MinorPublic());
            }
        }
        else
        {
            profile.MakePrivate();
        }

        profile.SetPublicSwitches(request.ShowBio ?? profile.ShowBio, request.ShowReports ?? profile.ShowPublicReports);
        profile.MarkUpdated(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<ProfileVisibilityView>.Success(await ProfileVisibilityReader.ReadAsync(db, links, urls, user.Id, now, cancellationToken));
    }
}

public sealed record GetProfileVisibilityRequest(Guid UserId);

public sealed class GetProfileVisibilityHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<GetProfileVisibilityRequest, Result<ProfileVisibilityView>>
{
    public async Task<Result<ProfileVisibilityView>> Handle(GetProfileVisibilityRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<ProfileVisibilityView>.Failure(SocialErrors.NotAvailable());
        }

        return Result<ProfileVisibilityView>.Success(await ProfileVisibilityReader.ReadAsync(db, links, urls, user.Id, now, cancellationToken));
    }
}

internal static class ProfileVisibilityReader
{
    public static async Task<ProfileVisibilityView> ReadAsync(
        IApplicationDbContext db,
        IPublicLinks links,
        IMediaUrlSigner urls,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var settings = await SocialRules.SettingsAsync(db, cancellationToken);
        var required = settings.GetValueOrDefault(SystemSettingKeys.ProfilePublicConsentVersion) ?? "2026-10-27";
        var text = settings.GetValueOrDefault(SystemSettingKeys.ProfilePublicConsentText) ?? "";
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var isPublic = profile?.Visibility == Situ.Domain.Profiles.ProfileVisibility.Public;
        var stranger = isPublic
            ? await PublicProfileReader.ForViewerAsync(db, links, urls, userId, userId, asStranger: true, now, cancellationToken)
            : null;
        var friend = await PublicProfileReader.ForViewerAsync(db, links, urls, userId, userId, asStranger: false, now, cancellationToken);
        return new ProfileVisibilityView(
            isPublic,
            profile?.PublicProfileConsentVersion,
            required,
            text,
            profile?.ShowBio ?? true,
            profile?.ShowPublicReports ?? true,
            stranger,
            friend!);
    }
}
