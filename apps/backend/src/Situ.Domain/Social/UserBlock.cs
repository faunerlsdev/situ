using Situ.Domain.Common;

namespace Situ.Domain.Social;

/// <summary>A user blocks another: they stop seeing each other's content and location (RF-PER-05).</summary>
public sealed class UserBlock : AuditableEntity
{
    private UserBlock()
    {
    }

    public Guid BlockerId { get; private set; }
    public Guid BlockedUserId { get; private set; }

    public static UserBlock Create(Guid blockerId, Guid blockedUserId)
    {
        Guard.That(blockerId != blockedUserId, "user_block.self", "You cannot block yourself.");

        return new UserBlock
        {
            BlockerId = Guard.NotEmpty(blockerId, nameof(blockerId)),
            BlockedUserId = Guard.NotEmpty(blockedUserId, nameof(blockedUserId)),
        };
    }
}
