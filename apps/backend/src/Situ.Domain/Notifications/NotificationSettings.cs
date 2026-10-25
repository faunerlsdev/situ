using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>Per-user notification preferences: radius around the user, quiet hours and global switches (RF-NOT-01..06).</summary>
public sealed class NotificationSettings : AuditableEntity
{
    public const double DefaultRadiusMeters = 1000;
    public const double MaxRadiusMeters = 10_000;
    public const string DefaultTimeZoneId = "America/Bogota";

    private NotificationSettings()
    {
        TimeZoneId = DefaultTimeZoneId;
    }

    public Guid UserId { get; private set; }

    public bool PushEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }
    public double NearbyRadiusMeters { get; private set; }
    public bool NotifyNearbyReports { get; private set; }
    public bool NotifyFriendReports { get; private set; }
    public bool NotifyFriendLives { get; private set; }
    public bool NotifyImportantNews { get; private set; }
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    public static NotificationSettings CreateDefault(Guid userId, string? timeZoneId, DateTimeOffset now)
    {
        var settings = new NotificationSettings
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            PushEnabled = true,
            EmailEnabled = true,
            NearbyRadiusMeters = DefaultRadiusMeters,
            NotifyNearbyReports = true,
            NotifyFriendReports = true,
            NotifyFriendLives = true,
            NotifyImportantNews = true,
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : Guard.NotEmpty(timeZoneId, nameof(timeZoneId), 64),
        };

        settings.MarkCreated(now);
        return settings;
    }

    public void Update(
        bool pushEnabled,
        bool emailEnabled,
        double nearbyRadiusMeters,
        bool notifyNearbyReports,
        bool notifyFriendReports,
        bool notifyFriendLives,
        bool notifyImportantNews,
        TimeOnly? quietStart,
        TimeOnly? quietEnd,
        string timeZoneId,
        DateTimeOffset now)
    {
        Guard.That((quietStart is null) == (quietEnd is null), "notification_settings.quiet_hours", "Quiet hours need both a start and an end.");

        PushEnabled = pushEnabled;
        EmailEnabled = emailEnabled;
        NearbyRadiusMeters = Guard.InRange(nearbyRadiusMeters, 100d, MaxRadiusMeters, nameof(nearbyRadiusMeters));
        NotifyNearbyReports = notifyNearbyReports;
        NotifyFriendReports = notifyFriendReports;
        NotifyFriendLives = notifyFriendLives;
        NotifyImportantNews = notifyImportantNews;
        QuietHoursStart = quietStart;
        QuietHoursEnd = quietEnd;
        TimeZoneId = Guard.NotEmpty(timeZoneId, nameof(timeZoneId), 64);
        MarkUpdated(now);
    }

    public bool IsQuietAt(TimeOnly localTime)
    {
        if (QuietHoursStart is not { } start || QuietHoursEnd is not { } end)
        {
            return false;
        }

        return start <= end
            ? localTime >= start && localTime < end
            : localTime >= start || localTime < end; // crosses midnight
    }
}
