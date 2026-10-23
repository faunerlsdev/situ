using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>One attempt to deliver a notification through a channel (push to a device, e-mail...).</summary>
public sealed class NotificationDelivery : AuditableEntity
{
    private NotificationDelivery()
    {
    }

    public Guid NotificationId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public Guid? DeviceId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public static NotificationDelivery Create(Guid notificationId, NotificationChannel channel, Guid? deviceId, DateTimeOffset now)
    {
        var delivery = new NotificationDelivery
        {
            NotificationId = Guard.NotEmpty(notificationId, nameof(notificationId)),
            Channel = channel,
            DeviceId = deviceId,
            Status = DeliveryStatus.Pending,
        };

        delivery.MarkCreated(now);
        return delivery;
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = DeliveryStatus.Sent;
        SentAt = now;
        Error = null;
    }

    public void MarkDelivered() => Status = DeliveryStatus.Delivered;

    public void MarkFailed(string error)
    {
        Status = DeliveryStatus.Failed;
        Error = Guard.Optional(error, nameof(error), 500);
    }

    public void Suppress(string reason)
    {
        Status = DeliveryStatus.Suppressed;
        Error = Guard.Optional(reason, nameof(reason), 500);
    }
}
