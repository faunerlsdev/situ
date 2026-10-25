using Situ.Domain.Common;

namespace Situ.Domain.Social;

public enum UserBlockScope
{
    /// <summary>Hides both people's content, ends the friendship and shared location.</summary>
    Full,

    /// <summary>The blocker stops seeing that person's anonymous content. The other person is not told.</summary>
    AnonymousContentOnly,
}

/// <summary>
/// A block between two accounts (RF-PER-05, RF-ANO-03).
/// Full is mutual in effect. AnonymousContentOnly only hides anonymous posts from the blocker.
/// </summary>
public sealed class UserBlock : AuditableEntity
{
    private UserBlock()
    {
    }

    public Guid BlockerId { get; private set; }
    public Guid BlockedUserId { get; private set; }
    public UserBlockScope Scope { get; private set; }

    /// <summary>Report or comment the blocker was looking at. Only set for an anonymous-content block.</summary>
    public ContentTargetType? OriginTargetType { get; private set; }

    public Guid? OriginTargetId { get; private set; }

    /// <summary>When that content was published, so the list can name the day without loading the content again.</summary>
    public DateTimeOffset? OriginAt { get; private set; }

    public static UserBlock Create(Guid blockerId, Guid blockedUserId, DateTimeOffset now) =>
        CreateFull(blockerId, blockedUserId, now);

    public static UserBlock CreateFull(Guid blockerId, Guid blockedUserId, DateTimeOffset now)
    {
        GuardPair(blockerId, blockedUserId);
        var block = new UserBlock
        {
            BlockerId = blockerId,
            BlockedUserId = blockedUserId,
            Scope = UserBlockScope.Full,
        };
        block.MarkCreated(now);
        return block;
    }

    public static UserBlock HideAnonymousContent(
        Guid blockerId,
        Guid blockedUserId,
        ContentTargetType originTargetType,
        Guid originTargetId,
        DateTimeOffset originAt,
        DateTimeOffset now)
    {
        GuardPair(blockerId, blockedUserId);
        Guard.That(
            originTargetType is ContentTargetType.Report or ContentTargetType.Comment,
            "user_block.origin",
            "An anonymous block starts from a report or a comment.");
        var block = new UserBlock
        {
            BlockerId = blockerId,
            BlockedUserId = blockedUserId,
            Scope = UserBlockScope.AnonymousContentOnly,
            OriginTargetType = originTargetType,
            OriginTargetId = Guard.NotEmpty(originTargetId, nameof(originTargetId)),
            OriginAt = originAt,
        };
        block.MarkCreated(now);
        return block;
    }

    private static void GuardPair(Guid blockerId, Guid blockedUserId)
    {
        Guard.That(blockerId != blockedUserId, "user_block.self", "You cannot block yourself.");
        Guard.NotEmpty(blockerId, nameof(blockerId));
        Guard.NotEmpty(blockedUserId, nameof(blockedUserId));
    }
}
