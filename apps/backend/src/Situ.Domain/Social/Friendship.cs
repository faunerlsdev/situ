using Situ.Domain.Common;

namespace Situ.Domain.Social;

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled,
}

/// <summary>Friend request between two users; once accepted it is the "network" used for notifications and location sharing (RF-AMI-03).</summary>
public sealed class Friendship : AggregateRoot
{
    private Friendship()
    {
    }

    public Guid RequesterId { get; private set; }
    public Guid AddresseeId { get; private set; }
    public FriendshipStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsAccepted => Status == FriendshipStatus.Accepted;

    public static Friendship Request(Guid requesterId, Guid addresseeId, DateTimeOffset now)
    {
        Guard.NotEmpty(requesterId, nameof(requesterId));
        Guard.NotEmpty(addresseeId, nameof(addresseeId));
        Guard.That(requesterId != addresseeId, "friendship.self", "You cannot send a friend request to yourself.");

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending,
            RequestedAt = now,
        };

        friendship.MarkCreated(now);
        friendship.Raise(new FriendRequestedEvent(friendship.Id, requesterId, addresseeId));
        return friendship;
    }

    public bool Involves(Guid userId) => RequesterId == userId || AddresseeId == userId;

    public Guid OtherParty(Guid userId)
    {
        Guard.That(Involves(userId), "friendship.not_involved", "The user is not part of this friendship.");
        return userId == RequesterId ? AddresseeId : RequesterId;
    }

    /// <summary>Only the addressee can accept.</summary>
    public void Accept(Guid byUserId, DateTimeOffset now)
    {
        EnsurePendingFor(byUserId, AddresseeId);
        Status = FriendshipStatus.Accepted;
        RespondedAt = now;
        Raise(new FriendshipAcceptedEvent(Id, RequesterId, AddresseeId));
    }

    /// <summary>Only the addressee can decline.</summary>
    public void Decline(Guid byUserId, DateTimeOffset now)
    {
        EnsurePendingFor(byUserId, AddresseeId);
        Status = FriendshipStatus.Declined;
        RespondedAt = now;
    }

    /// <summary>Ends an accepted or pending friendship because one side blocked the other. Does not restore it later.</summary>
    public void Sever(DateTimeOffset now)
    {
        if (Status is FriendshipStatus.Declined or FriendshipStatus.Cancelled)
        {
            return;
        }

        Status = FriendshipStatus.Cancelled;
        RespondedAt = now;
        MarkUpdated(now);
    }

    /// <summary>Only the requester can cancel a pending request.</summary>
    public void Cancel(Guid byUserId, DateTimeOffset now)
    {
        EnsurePendingFor(byUserId, RequesterId);
        Status = FriendshipStatus.Cancelled;
        RespondedAt = now;
    }

    private void EnsurePendingFor(Guid byUserId, Guid expectedUserId)
    {
        Guard.That(Status == FriendshipStatus.Pending, "friendship.not_pending", "This request is no longer pending.");
        Guard.That(byUserId == expectedUserId, "friendship.not_allowed", "This user cannot perform that action on the request.");
    }
}

public sealed record FriendRequestedEvent(Guid FriendshipId, Guid RequesterId, Guid AddresseeId) : DomainEvent;

public sealed record FriendshipAcceptedEvent(Guid FriendshipId, Guid RequesterId, Guid AddresseeId) : DomainEvent;
