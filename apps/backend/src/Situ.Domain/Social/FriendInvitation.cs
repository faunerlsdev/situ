using Situ.Domain.Common;

namespace Situ.Domain.Social;

public enum FriendInvitationStatus
{
    Pending,
    Accepted,
    Expired,
    Cancelled,
}

/// <summary>
/// Invitation sent by e-mail to someone who may not have an account yet (RF-AMI-02).
/// When the invitee registers through the link, a friend request is created automatically.
/// </summary>
public sealed class FriendInvitation : AuditableEntity
{
    private static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(30);

    private FriendInvitation()
    {
        Email = null!;
        TokenHash = null!;
    }

    public Guid InviterId { get; private set; }
    public string Email { get; private set; }
    public string TokenHash { get; private set; }
    public FriendInvitationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    public static FriendInvitation Create(Guid inviterId, string email, string tokenHash, DateTimeOffset now, TimeSpan? validFor = null)
    {
        var invitation = new FriendInvitation
        {
            InviterId = Guard.NotEmpty(inviterId, nameof(inviterId)),
            Email = Common.Email.Create(email).Value,
            TokenHash = Guard.NotEmpty(tokenHash, nameof(tokenHash)),
            Status = FriendInvitationStatus.Pending,
            ExpiresAt = now.Add(validFor ?? DefaultValidity),
        };

        invitation.MarkCreated(now);
        return invitation;
    }

    public bool IsOpen(DateTimeOffset now) => Status == FriendInvitationStatus.Pending && now < ExpiresAt;

    public void Accept(Guid userId, DateTimeOffset now)
    {
        Guard.That(IsOpen(now), "friend_invitation.not_open", "This invitation is no longer valid.");
        Guard.That(userId != InviterId, "friend_invitation.self", "You cannot accept your own invitation.");

        AcceptedByUserId = Guard.NotEmpty(userId, nameof(userId));
        AcceptedAt = now;
        Status = FriendInvitationStatus.Accepted;
    }

    public void Cancel()
    {
        if (Status == FriendInvitationStatus.Pending)
        {
            Status = FriendInvitationStatus.Cancelled;
        }
    }

    public void ExpireIfDue(DateTimeOffset now)
    {
        if (Status == FriendInvitationStatus.Pending && now >= ExpiresAt)
        {
            Status = FriendInvitationStatus.Expired;
        }
    }
}
