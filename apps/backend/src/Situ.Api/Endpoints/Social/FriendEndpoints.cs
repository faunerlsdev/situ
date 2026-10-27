using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles.ProfileVisibility;
using Situ.Application.Features.Social.FriendRequests;
using Situ.Application.Features.Social.GetPublicProfile;
using Situ.Application.Features.Social.InviteFriend;
using Situ.Application.Features.Social.SearchUsers;

namespace Situ.Api.Endpoints.Social;

public sealed class FriendEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/search", Search)
            .WithName("SearchUsers")
            .WithTags("Friends")
            .Produces<SearchUsersResponse>()
            .RequireRateLimiting("user-search")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/users/{userName}", Profile)
            .WithName("GetPublicProfile")
            .WithTags("Friends")
            .Produces<PublicProfileView>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/friends/requests", Send)
            .WithName("SendFriendRequest")
            .WithTags("Friends")
            .Produces<SendFriendRequestResponse>()
            .RequireRateLimiting("interaction-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/friends/requests/{id:guid}/accept", Accept)
            .WithName("AcceptFriendRequest")
            .WithTags("Friends")
            .Produces<FriendRequestActionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/friends/requests/{id:guid}/reject", Reject)
            .WithName("RejectFriendRequest")
            .WithTags("Friends")
            .Produces<FriendRequestActionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/friends/requests/{id:guid}/cancel", Cancel)
            .WithName("CancelFriendRequest")
            .WithTags("Friends")
            .Produces<FriendRequestActionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapDelete("/friends/{userId:guid}", Remove)
            .WithName("RemoveFriend")
            .WithTags("Friends")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/friends", List)
            .WithName("ListFriends")
            .WithTags("Friends")
            .Produces<ListFriendsResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/friends/requests", Requests)
            .WithName("ListFriendRequests")
            .WithTags("Friends")
            .Produces<ListFriendRequestsResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/friends/invitations", Invite)
            .WithName("InviteFriendByEmail")
            .WithTags("Friends")
            .Produces<InviteFriendByEmailResponse>()
            .RequireRateLimiting("interaction-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/friends/invitations/accept", AcceptInvite)
            .WithName("AcceptFriendInvitation")
            .WithTags("Friends")
            .Produces<AcceptFriendInvitationResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/me/profile-visibility", GetVisibility)
            .WithName("GetProfileVisibility")
            .WithTags("Profile")
            .Produces<ProfileVisibilityView>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPut("/me/profile-visibility", SetVisibility)
            .WithName("SetProfileVisibility")
            .WithTags("Profile")
            .Produces<ProfileVisibilityView>()
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> Search(string? userName, ICurrentUser current, IHandler<SearchUsersRequest, Result<SearchUsersResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SearchUsersRequest(userId, userName ?? ""), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Profile(string userName, ICurrentUser current, IHandler<GetPublicProfileRequest, Result<PublicProfileView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new GetPublicProfileRequest(userId, userName), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Send(SendFriendRequestBody body, ICurrentUser current, IHandler<SendFriendRequestRequest, Result<SendFriendRequestResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SendFriendRequestRequest(userId, body.UserId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Accept(Guid id, ICurrentUser current, IHandler<AcceptFriendRequest, Result<FriendRequestActionResponse>> handler, CancellationToken cancellationToken) =>
        await Act(current, id, (userId, requestId) => handler.Handle(new AcceptFriendRequest(userId, requestId), cancellationToken));

    private static async Task<IResult> Reject(Guid id, ICurrentUser current, IHandler<RejectFriendRequest, Result<FriendRequestActionResponse>> handler, CancellationToken cancellationToken) =>
        await Act(current, id, (userId, requestId) => handler.Handle(new RejectFriendRequest(userId, requestId), cancellationToken));

    private static async Task<IResult> Cancel(Guid id, ICurrentUser current, IHandler<CancelFriendRequest, Result<FriendRequestActionResponse>> handler, CancellationToken cancellationToken) =>
        await Act(current, id, (userId, requestId) => handler.Handle(new CancelFriendRequest(userId, requestId), cancellationToken));

    private static async Task<IResult> Act(ICurrentUser current, Guid id, Func<Guid, Guid, Task<Result<FriendRequestActionResponse>>> handle)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handle(userId, id)).ToHttpResult();
    }

    private static async Task<IResult> Remove(Guid userId, ICurrentUser current, IHandler<RemoveFriendRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } me)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new RemoveFriendRequest(me, userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> List(ICurrentUser current, IHandler<ListFriendsRequest, Result<ListFriendsResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new ListFriendsRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Requests(ICurrentUser current, IHandler<ListFriendRequestsRequest, Result<ListFriendRequestsResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new ListFriendRequestsRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Invite(InviteBody body, ICurrentUser current, IHandler<InviteFriendByEmailRequest, Result<InviteFriendByEmailResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new InviteFriendByEmailRequest(userId, body.Email), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> AcceptInvite(TokenBody body, ICurrentUser current, IHandler<AcceptFriendInvitationRequest, Result<AcceptFriendInvitationResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new AcceptFriendInvitationRequest(userId, body.Token), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> GetVisibility(ICurrentUser current, IHandler<GetProfileVisibilityRequest, Result<ProfileVisibilityView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new GetProfileVisibilityRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> SetVisibility(VisibilityBody body, ICurrentUser current, IHandler<SetProfileVisibilityRequest, Result<ProfileVisibilityView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SetProfileVisibilityRequest(userId, body.Public, body.ConsentVersion, body.ShowBio, body.ShowReports), cancellationToken)).ToHttpResult();
    }

    private sealed record InviteBody(string Email);

    private sealed record TokenBody(string Token);

    private sealed record VisibilityBody(bool Public, string? ConsentVersion, bool? ShowBio, bool? ShowReports);
}
