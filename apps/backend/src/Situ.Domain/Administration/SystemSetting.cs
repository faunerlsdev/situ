using Situ.Domain.Common;

namespace Situ.Domain.Administration;

/// <summary>Key/value configuration editable from the admin panel, without a release (RF-ADM-06).</summary>
public sealed class SystemSetting : AuditableEntity
{
    private SystemSetting()
    {
        Key = null!;
        Value = null!;
    }

    public string Key { get; private set; }
    public string Value { get; private set; }
    public string? Description { get; private set; }

    public static SystemSetting Create(string key, string value, string? description, DateTimeOffset now)
    {
        var setting = new SystemSetting
        {
            Key = Guard.NotEmpty(key, nameof(key), 100),
            Value = Guard.NotEmpty(value, nameof(value), 1000),
            Description = Guard.Optional(description, nameof(description), 300),
        };

        setting.MarkCreated(now);
        return setting;
    }

    public void ChangeValue(string value, DateTimeOffset now)
    {
        Value = Guard.NotEmpty(value, nameof(value), 1000);
        MarkUpdated(now);
    }
}

/// <summary>Well-known keys and their defaults.</summary>
public static class SystemSettingKeys
{
    public const string LiveRecordingRetentionDays = "live.recording_retention_days";
    public const string LiveInternalCopyRetentionDays = "live.internal_copy_retention_days";
    public const string ReportRetentionDays = "report.retention_days";
    public const string AnonymousDisplayName = "anonymity.display_name";
    public const string DefaultNotificationRadiusMeters = "notifications.default_radius_meters";
    public const string MaxDevicesFree = "devices.max_free";
    public const string LoginMaxFailedAttempts = "auth.max_failed_attempts";
    public const string LoginLockoutMinutes = "auth.lockout_minutes";
    public const string GuardianMaxSendsPerMinorPerDay = "guardian.max_sends_per_minor_per_day";
    public const string GuardianMinMinutesBetweenSends = "guardian.min_minutes_between_sends";
    public const string GuardianMaxEmailChangesPerDay = "guardian.max_email_changes_per_day";
    public const string GuardianMaxSendsPerEmailPerHour = "guardian.max_sends_per_email_per_hour";
    public const string GuardianMaxSendsPerEmailPerDay = "guardian.max_sends_per_email_per_day";
    public const string GuardianMaxSendsPerIpPerHour = "guardian.max_sends_per_ip_per_hour";
    public const string GuardianMaxCodeAttempts = "guardian.max_code_attempts";
    public const string GuardianMaxCodeResendsPer15Minutes = "guardian.max_code_resends_per_15_minutes";

    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        [LiveRecordingRetentionDays] = "60",
        [LiveInternalCopyRetentionDays] = "7",
        [ReportRetentionDays] = "365",
        [AnonymousDisplayName] = "Situero anónimo",
        [DefaultNotificationRadiusMeters] = "1000",
        [MaxDevicesFree] = "2",
        [LoginMaxFailedAttempts] = "5",
        [LoginLockoutMinutes] = "15",
        [GuardianMaxSendsPerMinorPerDay] = "5",
        [GuardianMinMinutesBetweenSends] = "2",
        [GuardianMaxEmailChangesPerDay] = "3",
        [GuardianMaxSendsPerEmailPerHour] = "3",
        [GuardianMaxSendsPerEmailPerDay] = "10",
        [GuardianMaxSendsPerIpPerHour] = "10",
        [GuardianMaxCodeAttempts] = "5",
        [GuardianMaxCodeResendsPer15Minutes] = "3",
    };
}
