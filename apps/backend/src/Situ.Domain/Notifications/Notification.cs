using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>An item in the user's notification inbox. Each channel attempt is a <see cref="NotificationDelivery"/>.</summary>
public sealed class Notification : AuditableEntity
{
    private Notification()
    {
        Title = null!;
        Body = null!;
    }

    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; }
    public string Body { get; private set; }
    public ContentTargetType? TargetType { get; private set; }
    public Guid? TargetId { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid recipientId,
        NotificationType type,
        string title,
        string body,
        ContentTargetType? targetType,
        Guid? targetId,
        DateTimeOffset now)
    {
        var notification = new Notification
        {
            RecipientId = Guard.NotEmpty(recipientId, nameof(recipientId)),
            Type = type,
            Title = Guard.NotEmpty(title, nameof(title), 120),
            Body = Guard.NotEmpty(body, nameof(body), 400),
            TargetType = targetType,
            TargetId = targetId,
        };

        notification.MarkCreated(now);
        return notification;
    }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
