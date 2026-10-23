using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>Reaction to a report, comment or live stream. One per user per target.</summary>
public sealed class Reaction : AuditableEntity
{
    private Reaction()
    {
    }

    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid UserId { get; private set; }
    public ReactionType Type { get; private set; }

    public static Reaction Create(ContentTargetType targetType, Guid targetId, Guid userId, ReactionType type, DateTimeOffset now)
    {
        var reaction = new Reaction
        {
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Type = type,
        };

        reaction.MarkCreated(now);
        return reaction;
    }

    public void Change(ReactionType type, DateTimeOffset now)
    {
        Type = type;
        MarkUpdated(now);
    }
}
