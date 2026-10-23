namespace Situ.Domain.Anonymity;

/// <summary>
/// How an anonymous author is shown to other users (RF-ANO-01..09). Applies to reports, comments and live streams.
/// The real author is always stored internally for moderation, reputation and legal requests.
/// </summary>
public static class AnonymousIdentity
{
    /// <summary>Default label; it can be overridden from system settings.</summary>
    public const string DefaultDisplayName = "Situero anónimo";

    /// <summary>
    /// "Situero anónimo" or, inside one conversation, "Situero anónimo 2".
    /// The index is per post and is never reused across posts, so nobody can be tracked.
    /// </summary>
    public static string DisplayName(int? index, string baseName = DefaultDisplayName) =>
        index is { } i ? $"{baseName} {i}" : baseName;

    /// <summary>"Miembro desde [mes y año]": only month and year are exposed, never the exact day (RF-ANO-02).</summary>
    public static (int Year, int Month) MemberSince(DateTimeOffset accountCreatedAt) =>
        (accountCreatedAt.Year, accountCreatedAt.Month);
}
