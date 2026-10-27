using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Social.SearchUsers;

public sealed record SearchUsersRequest(Guid UserId, string UserName);

public sealed record UserCard(Guid UserId, string UserName, string DisplayName, string? AvatarUrl);

public sealed record SearchUsersResponse(IReadOnlyList<UserCard> Users);

public sealed class SearchUsersValidator : AbstractValidator<SearchUsersRequest>
{
    public SearchUsersValidator()
    {
        RuleFor(request => request.UserName).NotEmpty().MinimumLength(3).MaximumLength(30);
    }
}

public sealed class SearchUsersHandler(IApplicationDbContext db, IClock clock, IPublicLinks links, IMediaUrlSigner urls)
    : IHandler<SearchUsersRequest, Result<SearchUsersResponse>>
{
    public async Task<Result<SearchUsersResponse>> Handle(SearchUsersRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var settings = await SocialRules.SettingsAsync(db, cancellationToken);
        var limit = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.FriendsSearchPerMinute, 10));
        var since = now.AddMinutes(-1);
        var recent = await db.AuditLogs.CountAsync(
            item => item.ActorUserId == request.UserId && item.Action == "friends.search" && item.OccurredAt >= since,
            cancellationToken);
        if (recent >= limit)
        {
            return Result<SearchUsersResponse>.Failure(SocialErrors.SearchLimited());
        }

        Audit.Write(db, request.UserId, "friends.search", "User", request.UserId, null, now);
        await db.SaveChangesAsync(cancellationToken);

        if (!UserName.TryCreate(request.UserName, out var userName))
        {
            return Result<SearchUsersResponse>.Success(new SearchUsersResponse([]));
        }

        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        var other = await db.Users.FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);
        if (me is null || other is null || other.Id == me.Id || !SocialRules.IsVisibleAccount(other))
        {
            return Result<SearchUsersResponse>.Success(new SearchUsersResponse([]));
        }

        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        if (!SocialRules.PairAllowed(me, other, now, allow)
            || await SocialRules.FullBlockBetweenAsync(db, me.Id, other.Id, cancellationToken))
        {
            return Result<SearchUsersResponse>.Success(new SearchUsersResponse([]));
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == other.Id, cancellationToken);
        var card = new UserCard(
            other.Id,
            other.UserName.Value,
            ProfileCards.DisplayName(other, profile),
            ProfileCards.Avatar(links, urls, other, profile, now));
        return Result<SearchUsersResponse>.Success(new SearchUsersResponse([card]));
    }
}
