using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Social.GetPublicProfile;

public sealed record GetPublicProfileRequest(Guid ViewerId, string UserName);

public sealed record PublicReportCard(Guid Id, string Excerpt, DateTimeOffset CreatedAt);

public sealed record PublicProfileView(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    int MemberSinceYear,
    int MemberSinceMonth,
    bool SharesLocation,
    bool IsFriend,
    IReadOnlyList<PublicReportCard> Reports);

public sealed class GetPublicProfileHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<GetPublicProfileRequest, Result<PublicProfileView>>
{
    public async Task<Result<PublicProfileView>> Handle(GetPublicProfileRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (!UserName.TryCreate(request.UserName, out var userName))
        {
            return Result<PublicProfileView>.Failure(SocialErrors.NotAvailable());
        }

        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ViewerId, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);
        if (viewer is null || user is null || !SocialRules.IsVisibleAccount(user))
        {
            return Result<PublicProfileView>.Failure(SocialErrors.NotAvailable());
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        var blocked = await SocialRules.FullBlockBetweenAsync(db, viewer.Id, user.Id, cancellationToken);
        var friends = await ReportAccess.FriendIdsAsync(db, viewer.Id, cancellationToken);
        var isFriend = friends.Contains(user.Id) && SocialRules.PairAllowed(viewer, user, now, allow) && !blocked;
        var isPublic = profile?.Visibility == ProfileVisibility.Public && !user.IsMinor(now);
        if (user.Id != viewer.Id && (blocked || !SocialRules.PairAllowed(viewer, user, now, allow) || (!isPublic && !isFriend)))
        {
            return Result<PublicProfileView>.Failure(SocialErrors.NotAvailable());
        }

        var view = await PublicProfileReader.ComposeAsync(db, links, urls, viewer, user, profile, isFriend, now, cancellationToken);
        return Result<PublicProfileView>.Success(view);
    }
}

internal static class PublicProfileReader
{
    public static async Task<PublicProfileView?> ForViewerAsync(
        IApplicationDbContext db,
        IPublicLinks links,
        IMediaUrlSigner urls,
        Guid subjectId,
        Guid viewerId,
        bool asStranger,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        var viewer = await db.Users.FirstAsync(item => item.Id == viewerId, cancellationToken);
        return await ComposeAsync(db, links, urls, viewer, user, profile, isFriend: !asStranger, now, cancellationToken);
    }

    public static async Task<PublicProfileView> ComposeAsync(
        IApplicationDbContext db,
        IPublicLinks links,
        IMediaUrlSigner urls,
        User viewer,
        User user,
        UserProfile? profile,
        bool isFriend,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var showBio = profile?.ShowBio ?? true;
        var showReports = profile?.ShowPublicReports ?? true;
        string? bio = showBio ? profile?.Bio : null;
        var reports = new List<PublicReportCard>();
        if (showReports)
        {
            var rows = await db.Reports
                .Where(item => item.AuthorId == user.Id && !item.IsAnonymous && item.Status == ReportStatus.Active && item.ExpiresAt > now)
                .OrderByDescending(item => item.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);
            foreach (var report in rows)
            {
                if (report.IsAnonymous)
                {
                    continue;
                }

                var visible = report.Visibility == ContentVisibility.Public
                    || (isFriend && report.Visibility == ContentVisibility.FriendsOnly)
                    || viewer.Id == user.Id;
                if (!visible)
                {
                    continue;
                }

                var excerpt = report.Description.Length <= 120 ? report.Description : report.Description[..120];
                reports.Add(new PublicReportCard(report.Id, excerpt, report.CreatedAt));
            }
        }

        return new PublicProfileView(
            user.Id,
            user.UserName.Value,
            ProfileCards.DisplayName(user, profile),
            ProfileCards.Avatar(links, urls, user, profile, now),
            bio,
            user.CreatedAt.Year,
            user.CreatedAt.Month,
            isFriend && (profile?.IsFriendSharingActive(now) ?? false),
            isFriend,
            reports);
    }
}
