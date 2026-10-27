namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Pushes one friend's location to one viewer's own connection.
/// Never a broadcast group: the caller already checked friendship, sharing, blocks and zones.
/// </summary>
public interface IFriendLocationPublisher
{
    Task UpdateAsync(Guid viewerId, FriendLocationPush push, CancellationToken cancellationToken);

    Task RemoveAsync(Guid viewerId, Guid subjectUserId, CancellationToken cancellationToken);
}

/// <summary>Rounded position for someone allowed to see it. Coordinates are omitted only when there is no stored point.</summary>
public sealed record FriendLocationPush(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    double? Latitude,
    double? Longitude,
    bool Live,
    DateTimeOffset? LastSeenAt);
