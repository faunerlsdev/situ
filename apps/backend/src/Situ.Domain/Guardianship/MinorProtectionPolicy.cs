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

    /// <summary>Minors can only stream to accepted friends (RF-MIN-09).</summary>
    public static bool CanStreamTo(int age, ContentVisibility visibility) =>
        age >= AdultAge || visibility == ContentVisibility.FriendsOnly;

    public static bool RequiresGuardianAuthorizationToStream(int age) => age < LiveStreamingGuardianAuthorizationAgeLimit;

    /// <summary>Adult-only report categories are hidden from minors (RF-MIN-06).</summary>
    public static bool CanUseAdultOnlyCategories(int age) => age >= AdultAge;

    /// <summary>No personalized ads or commercial profiling for minors (RF-MIN-06).</summary>
    public static bool CanReceivePersonalizedAds(int age) => age >= AdultAge;
}
