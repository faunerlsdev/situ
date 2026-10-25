using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Profiles;

namespace Situ.Application.Features.Profiles.UpdateMyProfile;

public sealed record UpdateMyProfileRequest(
    Guid UserId,
    string? UserName,
    string? DisplayName,
    string? Bio,
    Guid? HomeCityId);

public sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.DisplayName).MaximumLength(UserProfile.DisplayNameMaxLength);
        RuleFor(request => request.Bio).MaximumLength(UserProfile.BioMaxLength);
    }
}

public sealed class UpdateMyProfileHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateMyProfileRequest, Result>
{
    public async Task<Result> Handle(UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        var settings = await ProfileRules.SettingsAsync(db, cancellationToken);
        var blocked = ProfileRules.List(settings, Domain.Administration.SystemSettingKeys.ProfileBlockedTerms);
        var reserved = ProfileRules.List(settings, Domain.Administration.SystemSettingKeys.ProfileReservedUserNames);

        if (request.UserName is not null)
        {
            var renamed = await RenameAsync(user, request.UserName, reserved, blocked, settings, now, cancellationToken);
            if (renamed.IsFailure)
            {
                return renamed;
            }
        }

        if (request.DisplayName is not null && ProfileRules.ContainsBlocked(request.DisplayName, blocked))
        {
            return Result.Failure(ProfileRules.ContentRejected());
        }

        if (request.Bio is not null && ProfileRules.ContainsBlocked(request.Bio, blocked))
        {
            return Result.Failure(ProfileRules.ContentRejected());
        }

        if (request.HomeCityId is { } cityId)
        {
            var moved = await MoveHomeCityAsync(user, cityId, now, cancellationToken);
            if (moved.IsFailure)
            {
                return moved;
            }
        }

        if (request.DisplayName is not null || request.Bio is not null)
        {
            var profile = await ProfileAsync(user, cancellationToken);
            try
            {
                profile.UpdateDetails(
                    request.DisplayName ?? profile.DisplayName,
                    request.Bio is null ? profile.Bio : request.Bio);
            }
            catch (DomainException exception)
            {
                return Result.Failure(Error.Validation(exception.Code, "Revisa el nombre y la bio."));
            }

            profile.MarkUpdated(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> RenameAsync(
        Domain.Identity.User user,
        string requested,
        IReadOnlySet<string> reserved,
        IReadOnlySet<string> blocked,
        IReadOnlyDictionary<string, string> settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!UserName.TryCreate(requested, out var userName))
        {
            return Result.Failure(IdentityErrors.InvalidUserName());
        }

        if (userName.Value == user.UserName.Value)
        {
            return Result.Success();
        }

        if (reserved.Contains(userName.Value) || ProfileRules.ContainsBlocked(userName.Value, blocked))
        {
            return Result.Failure(reserved.Contains(userName.Value)
                ? IdentityErrors.UserNameTaken()
                : ProfileRules.ContentRejected());
        }

        var days = Math.Max(1, ProfileRules.Int(settings, Domain.Administration.SystemSettingKeys.ProfileUserNameChangeDays, 30));
        if (user.UserNameChangedAt is { } changed)
        {
            var availableAt = changed.AddDays(days);
            if (availableAt > now)
            {
                return Result.Failure(ProfileRules.UserNameCoolingDown(availableAt));
            }
        }

        var taken = await db.Users.AnyAsync(
            item => item.Id != user.Id && item.UserName == userName,
            cancellationToken);
        if (taken)
        {
            return Result.Failure(IdentityErrors.UserNameTaken());
        }

        user.ChangeUserName(userName, now);
        return Result.Success();
    }

    private async Task<Result> MoveHomeCityAsync(
        Domain.Identity.User user,
        Guid cityId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (cityId == user.CityId)
        {
            return Result.Success();
        }

        var city = await db.Cities.FirstOrDefaultAsync(item => item.Id == cityId && item.IsEnabled, cancellationToken);
        if (city is null)
        {
            return Result.Failure(IdentityErrors.CityUnavailable());
        }

        var region = await db.Regions.FirstOrDefaultAsync(item => item.Id == city.RegionId && item.IsEnabled, cancellationToken);
        var country = region is null
            ? null
            : await db.Countries.FirstOrDefaultAsync(item => item.Id == city.CountryId && item.IsEnabled, cancellationToken);
        if (region is null || country is null || region.CountryId != country.Id)
        {
            return Result.Failure(IdentityErrors.CityUnavailable());
        }

        user.ChangeInterestLocation(country.Id, region.Id, city.Id);
        user.MarkUpdated(now);
        return Result.Success();
    }

    private async Task<UserProfile> ProfileAsync(Domain.Identity.User user, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = UserProfile.CreateDefault(user.Id, user.GivenNames.Value);
        profile.MarkCreated(clock.UtcNow);
        db.UserProfiles.Add(profile);
        return profile;
    }
}
