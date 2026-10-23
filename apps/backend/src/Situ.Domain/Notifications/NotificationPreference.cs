using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>Switch for one notification type on one channel.</summary>
public sealed class NotificationPreference : AuditableEntity
{
    private NotificationPreference()
    {
    }

    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public bool IsEnabled { get; private set; }

    public static NotificationPreference Create(Guid userId, NotificationType type, NotificationChannel channel, bool isEnabled) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        Type = type,
        Channel = channel,
        IsEnabled = isEnabled,
    };

    public void Set(bool isEnabled) => IsEnabled = isEnabled;
}
