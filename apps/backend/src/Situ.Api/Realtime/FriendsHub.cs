using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Situ.Api.Auth;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Location;
using Situ.Application.Features.MapRealtime.AuthorizeMapConnection;

namespace Situ.Api.Realtime;

[Authorize(Policy = AuthorizationPolicies.User)]
public sealed class FriendsHub(
    IHandler<AuthorizeMapConnectionRequest, Result<AuthorizeMapConnectionResponse>> authorize,
    IHandler<ListFriendLocationsRequest, Result<GetMapPeopleResponse>> locations,
    ILogger<FriendsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (!Guid.TryParse(user?.FindFirst("sub")?.Value, out var userId))
        {
            Context.Abort();
            return;
        }

        Guid? deviceId = Guid.TryParse(user?.FindFirst("did")?.Value, out var parsed) ? parsed : null;
        var isStaff = user?.IsInRole(AuthorizationPolicies.Staff) == true
            || string.Equals(user?.FindFirst("role")?.Value, AuthorizationPolicies.Staff, StringComparison.Ordinal);
        DateTimeOffset? expiresAt = null;
        if (long.TryParse(user?.FindFirst("exp")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        var gate = await authorize.Handle(new AuthorizeMapConnectionRequest(userId, deviceId, isStaff, expiresAt), Context.ConnectionAborted);
        if (gate.IsFailure)
        {
            Context.Abort();
            return;
        }

        var snapshot = await locations.Handle(new ListFriendLocationsRequest(userId), Context.ConnectionAborted);
        if (snapshot.IsSuccess)
        {
            await Clients.Caller.SendAsync("locations", snapshot.Value!.People, Context.ConnectionAborted);
        }

        logger.LogInformation("Friends hub connected.");
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Friends hub disconnected.");
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value
        ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

public sealed record LocationRemovedSignal(Guid UserId);

public sealed class SignalRFriendLocationPublisher(IHubContext<FriendsHub> hub) : IFriendLocationPublisher
{
    public Task UpdateAsync(Guid viewerId, FriendLocationPush push, CancellationToken cancellationToken) =>
        hub.Clients.User(viewerId.ToString()).SendAsync("location", push, cancellationToken);

    public Task RemoveAsync(Guid viewerId, Guid subjectUserId, CancellationToken cancellationToken) =>
        hub.Clients.User(viewerId.ToString()).SendAsync("locationRemoved", new LocationRemovedSignal(subjectUserId), cancellationToken);
}
