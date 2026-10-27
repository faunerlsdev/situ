using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Domain.Social;

namespace Situ.Application.Features.Social.FriendRequests;

public sealed record SendFriendRequestBody(Guid UserId);

public sealed record SendFriendRequestRequest(Guid UserId, Guid AddresseeId);

public sealed record SendFriendRequestResponse(string Status);

public sealed class SendFriendRequestValidator : AbstractValidator<SendFriendRequestRequest>
{
    public SendFriendRequestValidator()
    {
        RuleFor(request => request.AddresseeId).NotEmpty();
    }
}

public sealed class SendFriendRequestHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SendFriendRequestRequest, Result<SendFriendRequestResponse>>
{
    public async Task<Result<SendFriendRequestResponse>> Handle(SendFriendRequestRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.AddresseeId)
        {
            return Result<SendFriendRequestResponse>.Failure(SocialErrors.Self());
        }

        var now = clock.UtcNow;
        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var other = await db.Users.FirstOrDefaultAsync(item => item.Id == request.AddresseeId, cancellationToken);
        if (me is null || !await CanSeeAsync(me, other, now, cancellationToken))
        {
            return Result<SendFriendRequestResponse>.Success(new SendFriendRequestResponse(SocialRules.Sent));
        }

        var existing = await PairAsync(me.Id, other!.Id, cancellationToken);
        var live = existing.FirstOrDefault(item => item.Status is FriendshipStatus.Pending or FriendshipStatus.Accepted);
        if (live is { Status: FriendshipStatus.Accepted })
        {
            return Result<SendFriendRequestResponse>.Failure(SocialErrors.AlreadyFriends());
        }

        if (live is { Status: FriendshipStatus.Pending, RequesterId: var requester } && requester == me.Id)
        {
            return Result<SendFriendRequestResponse>.Success(new SendFriendRequestResponse(SocialRules.Sent));
        }

        if (live is { Status: FriendshipStatus.Pending })
        {
            return Result<SendFriendRequestResponse>.Success(new SendFriendRequestResponse("incoming"));
        }

        if (await OverLimitAsync(me.Id, now, cancellationToken))
        {
            return Result<SendFriendRequestResponse>.Failure(SocialErrors.RateLimited());
        }

        var friendship = Friendship.Request(me.Id, other.Id, now);
        db.Friendships.Add(friendship);
        await NotifyRequestAsync(me, other.Id, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result<SendFriendRequestResponse>.Success(new SendFriendRequestResponse(SocialRules.Sent));
    }

    private async Task<bool> CanSeeAsync(User me, User? other, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (other is null || other.Status != UserStatus.Active)
        {
            return false;
        }

        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        return SocialRules.PairAllowed(me, other, now, allow)
            && !await SocialRules.FullBlockBetweenAsync(db, me.Id, other.Id, cancellationToken);
    }

    private async Task<List<Friendship>> PairAsync(Guid left, Guid right, CancellationToken cancellationToken) =>
        await db.Friendships
            .Where(item =>
                (item.RequesterId == left && item.AddresseeId == right)
                || (item.RequesterId == right && item.AddresseeId == left))
            .ToListAsync(cancellationToken);

    private async Task<bool> OverLimitAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = await SocialRules.SettingsAsync(db, cancellationToken);
        var pendingCap = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.FriendsMaxPending, 30));
        var dailyCap = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.FriendsMaxRequestsPerDay, 20));
        var pending = await db.Friendships.CountAsync(
            item => item.RequesterId == userId && item.Status == FriendshipStatus.Pending,
            cancellationToken);
        var since = now.AddHours(-24);
        var today = await db.Friendships.CountAsync(
            item => item.RequesterId == userId && item.RequestedAt >= since,
            cancellationToken);
        return pending >= pendingCap || today >= dailyCap;
    }

    private async Task NotifyRequestAsync(User me, Guid addresseeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == me.Id, cancellationToken);
        var name = ProfileCards.DisplayName(me, profile);
        await SocialRules.NotifyAsync(
            db,
            addresseeId,
            NotificationType.FriendRequest,
            "Solicitud de amistad",
            $"{name} (@{me.UserName.Value}) quiere ser tu amigo.",
            me.Id,
            now,
            cancellationToken);
    }
}

public sealed record FriendRequestActionRequest(Guid UserId, Guid RequestId);

public sealed record FriendRequestActionResponse(string Status);

public sealed class AcceptFriendRequestHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AcceptFriendRequest, Result<FriendRequestActionResponse>>
{
    public async Task<Result<FriendRequestActionResponse>> Handle(AcceptFriendRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var friendship = await db.Friendships.FirstOrDefaultAsync(item => item.Id == request.RequestId, cancellationToken);
        if (friendship is null || !friendship.Involves(request.UserId))
        {
            return Result<FriendRequestActionResponse>.Failure(SocialErrors.NotPending());
        }

        var me = await db.Users.FirstAsync(item => item.Id == request.UserId, cancellationToken);
        var other = await db.Users.FirstAsync(item => item.Id == friendship.OtherParty(request.UserId), cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        if (!SocialRules.PairAllowed(me, other, now, allow)
            || await SocialRules.FullBlockBetweenAsync(db, me.Id, other.Id, cancellationToken))
        {
            return Result<FriendRequestActionResponse>.Failure(SocialErrors.NotAllowed());
        }

        try
        {
            friendship.Accept(request.UserId, now);
        }
        catch (DomainException exception) when (exception.Code is "friendship.not_pending" or "friendship.not_allowed")
        {
            return Result<FriendRequestActionResponse>.Failure(
                exception.Code == "friendship.not_pending" ? SocialErrors.NotPending() : SocialErrors.NotAllowed());
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == me.Id, cancellationToken);
        await SocialRules.NotifyAsync(
            db,
            friendship.RequesterId,
            NotificationType.FriendAccepted,
            "Solicitud aceptada",
            $"{ProfileCards.DisplayName(me, profile)} (@{me.UserName.Value}) aceptó tu solicitud.",
            me.Id,
            now,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result<FriendRequestActionResponse>.Success(new FriendRequestActionResponse("accepted"));
    }
}

public sealed record AcceptFriendRequest(Guid UserId, Guid RequestId);

public sealed class RejectFriendRequestHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RejectFriendRequest, Result<FriendRequestActionResponse>>
{
    public async Task<Result<FriendRequestActionResponse>> Handle(RejectFriendRequest request, CancellationToken cancellationToken)
    {
        var friendship = await db.Friendships.FirstOrDefaultAsync(item => item.Id == request.RequestId, cancellationToken);
        if (friendship is null || !friendship.Involves(request.UserId))
        {
            return Result<FriendRequestActionResponse>.Failure(SocialErrors.NotPending());
        }

        try
        {
            friendship.Decline(request.UserId, clock.UtcNow);
        }
        catch (DomainException exception) when (exception.Code is "friendship.not_pending" or "friendship.not_allowed")
        {
            return Result<FriendRequestActionResponse>.Failure(
                exception.Code == "friendship.not_pending" ? SocialErrors.NotPending() : SocialErrors.NotAllowed());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<FriendRequestActionResponse>.Success(new FriendRequestActionResponse("rejected"));
    }
}

public sealed record RejectFriendRequest(Guid UserId, Guid RequestId);

public sealed class CancelFriendRequestHandler(IApplicationDbContext db, IClock clock)
    : IHandler<CancelFriendRequest, Result<FriendRequestActionResponse>>
{
    public async Task<Result<FriendRequestActionResponse>> Handle(CancelFriendRequest request, CancellationToken cancellationToken)
    {
        var friendship = await db.Friendships.FirstOrDefaultAsync(item => item.Id == request.RequestId, cancellationToken);
        if (friendship is null || !friendship.Involves(request.UserId))
        {
            return Result<FriendRequestActionResponse>.Failure(SocialErrors.NotPending());
        }

        try
        {
            friendship.Cancel(request.UserId, clock.UtcNow);
        }
        catch (DomainException exception) when (exception.Code is "friendship.not_pending" or "friendship.not_allowed")
        {
            return Result<FriendRequestActionResponse>.Failure(
                exception.Code == "friendship.not_pending" ? SocialErrors.NotPending() : SocialErrors.NotAllowed());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<FriendRequestActionResponse>.Success(new FriendRequestActionResponse("cancelled"));
    }
}

public sealed record CancelFriendRequest(Guid UserId, Guid RequestId);

public sealed record RemoveFriendRequest(Guid UserId, Guid FriendUserId);

public sealed class RemoveFriendHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RemoveFriendRequest, Result>
{
    public async Task<Result> Handle(RemoveFriendRequest request, CancellationToken cancellationToken)
    {
        var friendship = await db.Friendships.FirstOrDefaultAsync(
            item => item.Status == FriendshipStatus.Accepted
                && ((item.RequesterId == request.UserId && item.AddresseeId == request.FriendUserId)
                    || (item.RequesterId == request.FriendUserId && item.AddresseeId == request.UserId)),
            cancellationToken);
        if (friendship is null)
        {
            return Result.Failure(SocialErrors.NotFriend());
        }

        var now = clock.UtcNow;
        await InteractionAccess.SeverRelationshipAsync(db, request.UserId, request.FriendUserId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListFriendsRequest(Guid UserId);

public sealed record FriendListItem(Guid UserId, string UserName, string DisplayName, string? AvatarUrl, bool SharesLocation);

public sealed record ListFriendsResponse(IReadOnlyList<FriendListItem> Friends);

public sealed class ListFriendsHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<ListFriendsRequest, Result<ListFriendsResponse>>
{
    public async Task<Result<ListFriendsResponse>> Handle(ListFriendsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (me is null)
        {
            return Result<ListFriendsResponse>.Success(new ListFriendsResponse([]));
        }

        var rows = await db.Friendships
            .Where(item => item.Status == FriendshipStatus.Accepted && (item.RequesterId == me.Id || item.AddresseeId == me.Id))
            .ToListAsync(cancellationToken);
        var ids = rows.Select(item => item.OtherParty(me.Id)).Distinct().ToArray();
        var users = await db.Users.Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => ids.Contains(item.UserId)).ToListAsync(cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        var items = new List<FriendListItem>();
        foreach (var user in users)
        {
            if (!SocialRules.IsVisibleAccount(user)
                || !SocialRules.PairAllowed(me, user, now, allow)
                || await SocialRules.FullBlockBetweenAsync(db, me.Id, user.Id, cancellationToken))
            {
                continue;
            }

            var profile = profiles.FirstOrDefault(item => item.UserId == user.Id);
            items.Add(new FriendListItem(
                user.Id,
                user.UserName.Value,
                ProfileCards.DisplayName(user, profile),
                ProfileCards.Avatar(links, urls, user, profile, now),
                profile?.IsFriendSharingActive(now) ?? false));
        }

        return Result<ListFriendsResponse>.Success(new ListFriendsResponse(items.OrderBy(item => item.UserName).ToList()));
    }
}

public sealed record ListFriendRequestsRequest(Guid UserId);

public sealed record FriendRequestItem(Guid RequestId, string Direction, Guid UserId, string UserName, string DisplayName, string? AvatarUrl);

public sealed record ListFriendRequestsResponse(IReadOnlyList<FriendRequestItem> Incoming, IReadOnlyList<FriendRequestItem> Outgoing);

public sealed class ListFriendRequestsHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<ListFriendRequestsRequest, Result<ListFriendRequestsResponse>>
{
    public async Task<Result<ListFriendRequestsResponse>> Handle(ListFriendRequestsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (me is null)
        {
            return Result<ListFriendRequestsResponse>.Success(new ListFriendRequestsResponse([], []));
        }

        var rows = await db.Friendships
            .Where(item => item.Status == FriendshipStatus.Pending && (item.RequesterId == me.Id || item.AddresseeId == me.Id))
            .ToListAsync(cancellationToken);
        var ids = rows.Select(item => item.OtherParty(me.Id)).Distinct().ToArray();
        var users = await db.Users.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var profiles = await db.UserProfiles.Where(item => ids.Contains(item.UserId)).ToListAsync(cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        var incoming = new List<FriendRequestItem>();
        var outgoing = new List<FriendRequestItem>();
        foreach (var row in rows)
        {
            var otherId = row.OtherParty(me.Id);
            if (!users.TryGetValue(otherId, out var user)
                || !SocialRules.IsVisibleAccount(user)
                || !SocialRules.PairAllowed(me, user, now, allow)
                || await SocialRules.FullBlockBetweenAsync(db, me.Id, user.Id, cancellationToken))
            {
                continue;
            }

            var profile = profiles.FirstOrDefault(item => item.UserId == user.Id);
            var item = new FriendRequestItem(
                row.Id,
                row.AddresseeId == me.Id ? "incoming" : "outgoing",
                user.Id,
                user.UserName.Value,
                ProfileCards.DisplayName(user, profile),
                ProfileCards.Avatar(links, urls, user, profile, now));
            if (row.AddresseeId == me.Id)
            {
                incoming.Add(item);
            }
            else
            {
                outgoing.Add(item);
            }
        }

        return Result<ListFriendRequestsResponse>.Success(new ListFriendRequestsResponse(incoming, outgoing));
    }
}
