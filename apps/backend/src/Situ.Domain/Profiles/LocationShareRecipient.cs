using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>Friend selected to receive the user's real-time location when the sharing mode is "selected friends" (RF-UBI-03).</summary>
public sealed class LocationShareRecipient : AuditableEntity
{
    private LocationShareRecipient()
    {
    }

    public Guid UserId { get; private set; }
    public Guid FriendUserId { get; private set; }

    public static LocationShareRecipient Create(Guid userId, Guid friendUserId)
    {
        Guard.That(userId != friendUserId, "location_share.self", "You cannot share your location with yourself.");

        return new LocationShareRecipient
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            FriendUserId = Guard.NotEmpty(friendUserId, nameof(friendUserId)),
        };
    }
}
