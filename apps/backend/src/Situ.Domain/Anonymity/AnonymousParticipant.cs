using Situ.Domain.Common;

namespace Situ.Domain.Anonymity;

/// <summary>
/// Stable anonymous number of one user inside one target (a report, for comments).
/// The same person keeps one index per target. The index is not reused on another target.
/// </summary>
public sealed class AnonymousParticipant : AuditableEntity
{
    private AnonymousParticipant()
    {
    }

    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid UserId { get; private set; }
    public int Index { get; private set; }

    public static AnonymousParticipant Assign(
        ContentTargetType targetType,
        Guid targetId,
        Guid userId,
        int index,
        DateTimeOffset now)
    {
        Guard.That(index > 0, "anonymity.index_invalid", "The anonymous index starts at 1.");
        var participant = new AnonymousParticipant
        {
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Index = index,
        };
        participant.MarkCreated(now);
        return participant;
    }
}
