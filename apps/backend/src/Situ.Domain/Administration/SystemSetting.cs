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
    public const string ReportCatalogVersion = "reports.catalog_version";
    public const string ReportMaxImageBytes = "reports.max_image_bytes";
    public const string ReportMaxVideoBytes = "reports.max_video_bytes";
    public const string ReportMaxVideoSeconds = "reports.max_video_seconds";
    public const string ReportMaxImages = "reports.max_images";
    public const string ReportMaxVideos = "reports.max_videos";
    public const string ReportConfirmationRadiusMeters = "reports.confirmation_radius_meters";
    public const string ReportConfirmationExtensionPercent = "reports.confirmation_extension_percent";
    public const string ReportConfirmationMinExtensionMinutes = "reports.confirmation_min_extension_minutes";
    public const string ReportPendingMediaHours = "reports.pending_media_hours";
    public const string ReportDescriptionMaxLength = "reports.description_max_length";
    public const string PanelBrowseCopy = "panel.browse_copy";
    public const string PanelReportCopy = "panel.report_copy";
    public const string AppIosMinSupportedVersion = "app.ios.min_supported_version";
    public const string AppIosLatestVersion = "app.ios.latest_version";
    public const string AppAndroidMinSupportedVersion = "app.android.min_supported_version";
    public const string AppAndroidLatestVersion = "app.android.latest_version";
    public const string AppWebMinSupportedVersion = "app.web.min_supported_version";
    public const string AppWebLatestVersion = "app.web.latest_version";
    public const string ComplaintAutoReviewThreshold = "moderation.complaint_threshold";
    public const string AnonymousCommentsPerDay = "comments.max_anonymous_per_day";
    public const string AccountDeletionGraceDays = "account.deletion_grace_days";
    public const string NotificationMaxPushesPerHour = "notifications.max_pushes_per_hour";
    public const string NotificationMaxInterestCities = "notifications.max_interest_cities";
    public const string NotificationAlertLocationMinutes = "notifications.alert_location_ttl_minutes";
    public const string NotificationDispatchBatchSize = "notifications.dispatch_batch_size";
    public const string NotificationPushBatchSize = "notifications.push_batch_size";
    public const string ProfileUserNameChangeDays = "profile.username_change_days";
    public const string ProfileReservedUserNames = "profile.reserved_usernames";
    public const string ProfileBlockedTerms = "profile.blocked_terms";
    public const string ProfileAvatarMaxBytes = "profile.avatar_max_bytes";
    public const string LegalTermsUrl = "legal.terms_url";
    public const string LegalPrivacyUrl = "legal.privacy_url";
    public const string LegalSupportUrl = "legal.support_url";
    public const string PanelCopyByCountry = "panel.copy_by_country";

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
        [ReportCatalogVersion] = "1",
        [ReportMaxImageBytes] = "10485760",
        [ReportMaxVideoBytes] = "104857600",
        [ReportMaxVideoSeconds] = "60",
        [ReportMaxImages] = "5",
        [ReportMaxVideos] = "1",
        [ReportConfirmationRadiusMeters] = "2000",
        [ReportConfirmationExtensionPercent] = "25",
        [ReportConfirmationMinExtensionMinutes] = "10",
        [ReportPendingMediaHours] = "24",
        [ReportDescriptionMaxLength] = "1000",
        [PanelBrowseCopy] = "Quiero chismosear",
        [PanelReportCopy] = "Quiero reportar algo",
        [AppIosMinSupportedVersion] = "0.1.0",
        [AppIosLatestVersion] = "0.1.0",
        [AppAndroidMinSupportedVersion] = "0.1.0",
        [AppAndroidLatestVersion] = "0.1.0",
        [AppWebMinSupportedVersion] = "0.1.0",
        [AppWebLatestVersion] = "0.1.0",
        [ComplaintAutoReviewThreshold] = "3",
        [AnonymousCommentsPerDay] = "30",
        [AccountDeletionGraceDays] = "14",
        [NotificationMaxPushesPerHour] = "6",
        [NotificationMaxInterestCities] = "3",
        [NotificationAlertLocationMinutes] = "30",
        [NotificationDispatchBatchSize] = "50",
        [NotificationPushBatchSize] = "100",
        [ProfileUserNameChangeDays] = "30",
        [ProfileReservedUserNames] = "admin,administrador,soporte,support,situ,somossitu,moderador,moderator,ayuda,help,root,system,sistema,staff,oficial",
        [ProfileBlockedTerms] = "odio,nazi,porn,xxx,puta,mierda",
        [ProfileAvatarMaxBytes] = "5242880",
        [LegalTermsUrl] = "https://somossitu.com/terminos",
        [LegalPrivacyUrl] = "https://somossitu.com/privacidad",
        [LegalSupportUrl] = "https://somossitu.com/soporte",
        [PanelCopyByCountry] = "[]",
    };
}
