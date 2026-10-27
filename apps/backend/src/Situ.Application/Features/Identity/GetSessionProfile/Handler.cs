using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles;
using Situ.Domain.Administration;
using Situ.Domain.Guardianship;
using Situ.Domain.Legal;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;
using Situ.Domain.Profiles;
using Situ.Domain.Retention;

namespace Situ.Application.Features.Identity.GetSessionProfile;

public sealed record GetSessionProfileRequest(Guid UserId);

public sealed record SessionAvatarView(string Kind, string? Url);

public sealed record SessionHomeCityView(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    Guid CountryId,
    string CountryName);

public sealed record SessionPlanLimitsView(
    int? MaxReportsPerDay,
    int? MaxAnonymousPostsPerDay,
    int? MaxDevices,
    bool ShowsAds);

public sealed record SessionPlanView(string Code, string Tier, SessionPlanLimitsView Limits);

public sealed record SessionProfileView(string Visibility, bool ShareLocationWithFriends, bool PreferAnonymousReports);

public sealed record SessionGuardianPermissionsView(bool LiveStreaming, bool ShareLocationWithFriends);

public sealed record SessionGuardianView(string Status, SessionGuardianPermissionsView Permissions);

public sealed record GetSessionProfileResponse(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Bio,
    int MemberSinceYear,
    int MemberSinceMonth,
    string MaskedEmail,
    DateTimeOffset? CanChangeUserNameAt,
    SessionAvatarView Avatar,
    string Locale,
    bool EmailConfirmed,
    string Status,
    bool IsMinor,
    bool CanPublish,
    string? PublishBlockedReason,
    SessionHomeCityView HomeCity,
    SessionPlanView Plan,
    SessionProfileView Profile,
    SessionGuardianView? Guardian,
    DateTimeOffset? DeletionScheduledFor,
    string? Phone,
    bool AnonymityNoticeAccepted);

public sealed class GetSessionProfileHandler(
    IApplicationDbContext db,
    IClock clock,
    IMediaUrlSigner urls,
    IPublicLinks links)
    : IHandler<GetSessionProfileRequest, Result<GetSessionProfileResponse>>
{
    public async Task<Result<GetSessionProfileResponse>> Handle(
        GetSessionProfileRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<GetSessionProfileResponse>.Failure(Error.NotFound("session.not_found", "No encontramos la sesión."));
        }

        var city = await db.Cities.FirstOrDefaultAsync(item => item.Id == user.CityId, cancellationToken);
        var country = await db.Countries.FirstOrDefaultAsync(item => item.Id == user.CountryId, cancellationToken);
        if (city is null || country is null)
        {
            return Result<GetSessionProfileResponse>.Failure(Error.NotFound("session.city_not_found", "No encontramos tu ciudad de interés."));
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        var plan = await CurrentPlanAsync(user.Id, now, cancellationToken);
        var isMinor = user.IsMinor(now);
        SessionGuardianView? guardian = null;
        if (isMinor)
        {
            var consent = await db.GuardianConsents
                .Where(item => item.MinorUserId == user.Id)
                .OrderByDescending(item => item.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);
            guardian = new SessionGuardianView(
                consent?.Status.ToString() ?? "None",
                new SessionGuardianPermissionsView(
                    consent?.LiveStreamingAuthorized ?? false,
                    consent?.LocationSharingAuthorized ?? false));
        }

        var canPublish = user.CanPublish;
        DateTimeOffset? deletionScheduledFor = null;
        if (user.Status == UserStatus.DeletionRequested)
        {
            deletionScheduledFor = await db.AccountDeletionRequests
                .Where(item => item.UserId == user.Id && item.Status == AccountDeletionStatus.Scheduled)
                .OrderByDescending(item => item.ScheduledFor)
                .Select(item => (DateTimeOffset?)item.ScheduledFor)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var settings = await ProfileRules.SettingsAsync(db, cancellationToken);
        DateTimeOffset? canChangeUserNameAt = null;
        if (user.UserNameChangedAt is { } changed)
        {
            var days = Math.Max(1, ProfileRules.Int(settings, SystemSettingKeys.ProfileUserNameChangeDays, 30));
            var availableAt = changed.AddDays(days);
            if (availableAt > now)
            {
                canChangeUserNameAt = availableAt;
            }
        }

        return Result<GetSessionProfileResponse>.Success(new GetSessionProfileResponse(
            user.Id,
            user.UserName.Value,
            profile?.DisplayName ?? user.GivenNames.Value,
            profile?.Bio,
            user.CreatedAt.Year,
            user.CreatedAt.Month,
            ProfileRules.MaskEmail(user.Email.Value),
            canChangeUserNameAt,
            new SessionAvatarView(
                profile?.AvatarKind.ToString() ?? AvatarKind.Default.ToString(),
                AvatarUrl(user.Id, profile, now)),
            user.Locale,
            user.EmailConfirmedAt is not null,
            user.Status.ToString(),
            isMinor,
            canPublish,
            PublishBlockedReason(user),
            new SessionHomeCityView(city.Id, city.Name, city.Center.Latitude, city.Center.Longitude, country.Id, country.Name),
            new SessionPlanView(
                plan.Code,
                plan.Tier.ToString(),
                new SessionPlanLimitsView(plan.MaxReportsPerDay, plan.MaxAnonymousPostsPerDay, plan.MaxDevices, plan.ShowsAds)),
            new SessionProfileView(
                (profile?.Visibility ?? ProfileVisibility.Private).ToString(),
                profile?.IsFriendSharingActive(now) ?? false,
                profile?.PreferAnonymousReports ?? true),
            guardian,
            deletionScheduledFor,
            user.Phone?.Value,
            await db.ConsentRecords.AnyAsync(
                item => item.UserId == user.Id && item.DocumentType == LegalDocumentType.AnonymityNotice && item.RevokedAt == null,
                cancellationToken)));
    }

    private string? AvatarUrl(Guid userId, UserProfile? profile, DateTimeOffset now)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.AvatarUrl))
        {
            return null;
        }

        if (profile.AvatarKind == AvatarKind.Custom && profile.AvatarUrl.StartsWith("avatars/", StringComparison.Ordinal))
        {
            var signed = urls.Sign(userId, now.AddHours(6));
            return links.Absolute($"/api/v1/avatars/{userId}/content?exp={signed.ExpiresUnix}&sig={signed.Signature}");
        }

        return profile.AvatarUrl.StartsWith('/') ? links.Absolute(profile.AvatarUrl) : profile.AvatarUrl;
    }

    internal static string? PublishBlockedReason(User user)
    {
        if (user.CanPublish)
        {
            return null;
        }

        return user.Status switch
        {
            UserStatus.PendingEmailConfirmation => "email_not_confirmed",
            UserStatus.PendingGuardianConsent => "guardian_consent_pending",
            UserStatus.Suspended => "suspended",
            UserStatus.Banned => "banned",
            UserStatus.DeletionRequested => "deletion_requested",
            UserStatus.Deleted => "deleted",
            _ => null,
        };
    }

    private async Task<SubscriptionPlan> CurrentPlanAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CurrentPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
        if (subscription is not null && subscription.GrantsAccess(now))
        {
            var paid = await db.SubscriptionPlans.FirstOrDefaultAsync(item => item.Id == subscription.PlanId, cancellationToken);
            if (paid is not null)
            {
                return paid;
            }
        }

        var free = await db.SubscriptionPlans
            .Where(item => item.Tier == PlanTier.Free && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        return free ?? SubscriptionPlan.Create("free", "Free", "Plan de respaldo.", PlanTier.Free, 0, true, 2, null, null, null, null, false);
    }
}
