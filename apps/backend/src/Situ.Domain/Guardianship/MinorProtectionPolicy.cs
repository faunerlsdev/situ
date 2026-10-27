using Situ.Domain.Common;

namespace Situ.Domain.Guardianship;

/// <summary>
/// Pure rules that protect users under 18 (RF-MIN-01..12). The minimum registration age (14 today) is NOT here:
/// it is a per-country parameter (<c>Country.MinimumAge</c>) so it can change if the law does.
/// </summary>
public static class MinorProtectionPolicy
{
    public const int AdultAge = 18;

    /// <summary>Under this age a live stream also needs a specific guardian authorization (same bar Meta uses).</summary>
    public const int LiveStreamingGuardianAuthorizationAgeLimit = 16;

    public static readonly TimeSpan DefaultGuardianResponseWindow = TimeSpan.FromDays(7);

    public static readonly TimeSpan GuardianEmailVerificationLifetime = TimeSpan.FromHours(24);

    public static readonly TimeSpan GuardianEmailCodeLifetime = TimeSpan.FromMinutes(10);

    public static bool IsMinor(int age) => age < AdultAge;

    /// <summary>Minors keep a private profile and never appear on the public map (RF-MIN-04).</summary>
    public static bool CanHavePublicProfile(int age) => age >= AdultAge;

    /// <summary>
    /// When <paramref name="allowAdultMinor"/> is false, an adult and a minor cannot be friends.
    /// Minors only relate to minors. The flag is a system setting and defaults to false.
    /// </summary>
    public static bool CanBeFriends(int ageA, int ageB, bool allowAdultMinor) =>
        allowAdultMinor || IsMinor(ageA) == IsMinor(ageB);

    /// <summary>A minor cannot upload their own photo as an avatar. Gallery icons stay available.</summary>
    public static bool CanUploadProfilePhoto(int age) => age >= AdultAge;

    /// <summary>Minors can only stream to accepted friends (RF-MIN-09).</summary>
    public static bool CanStreamTo(int age, ContentVisibility visibility) =>
        age >= AdultAge || visibility == ContentVisibility.FriendsOnly;

    public static bool RequiresGuardianAuthorizationToStream(int age) => age < LiveStreamingGuardianAuthorizationAgeLimit;

    /// <summary>Adult-only report categories are hidden from minors (RF-MIN-06).</summary>
    public static bool CanUseAdultOnlyCategories(int age) => age >= AdultAge;

    /// <summary>
    /// A minor may publish publicly only when the report is anonymous.
    /// Otherwise the report has to be visible to accepted friends only.
    /// Adults are not restricted. The caller rejects the request; visibility is not rewritten in silence.
    /// </summary>
    public static bool CanPublishReport(int age, ContentVisibility visibility, bool isAnonymous) =>
        !IsMinor(age) || visibility == ContentVisibility.FriendsOnly || isAnonymous;

    /// <summary>
    /// A minor may comment with their name only on a friends-only report.
    /// On a public report the comment has to be anonymous. Adults are not restricted.
    /// The caller rejects the request; the comment is not rewritten in silence.
    /// </summary>
    public static bool CanComment(int age, ContentVisibility reportVisibility, bool isAnonymous) =>
        CanPublishReport(age, reportVisibility, isAnonymous);

    /// <summary>No personalized ads or commercial profiling for minors (RF-MIN-06).</summary>
    public static bool CanReceivePersonalizedAds(int age) => age >= AdultAge;

    /// <summary>
    /// A minor may store an approximate location for proximity alerts only when the guardian
    /// authorized location sharing. Adults are not restricted. Without it, the minor still
    /// receives city-of-interest alerts.
    /// </summary>
    public static bool CanShareProximityLocation(int age, bool guardianAuthorizedLocation) =>
        !IsMinor(age) || guardianAuthorizedLocation;
}
