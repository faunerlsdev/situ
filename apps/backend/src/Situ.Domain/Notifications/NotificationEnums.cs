namespace Situ.Domain.Notifications;

public enum NotificationType
{
    NearbyReport,
    FriendReport,
    FriendLiveStarted,
    ImportantNews,
    FriendRequest,
    FriendAccepted,
    Comment,
    Reaction,
    ReportConfirmed,
    SecurityAlert,
    GuardianConsent,
    Subscription,
    OrganizationAlert,
    System,
}

public enum NotificationChannel
{
    Push,
    Email,
    InApp,
}

public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Suppressed,
}
