using Situ.Domain.Common;
using Situ.Domain.Guardianship;

namespace Situ.Domain.Profiles;

public enum AvatarKind
{
    Default,
    Gallery,
    Custom,
}

public enum ProfileVisibility
{
    Private,
    Public,
}

public enum LocationSharingMode
{
    AllFriends,
    SelectedFriends,
}

/// <summary>
/// Public face of a user and its privacy switches (RF-PER-01..06, RF-UBI-01..11).
/// Two independent location switches:
/// (1) <see cref="ShowRealtimeLocation"/> — only meaningful for public profiles, on by default when public;
/// (2) <see cref="ShareLocationWithFriends"/> — any profile, off by default, visible only to accepted friends.
/// </summary>
public sealed class UserProfile : AggregateRoot
{
    private UserProfile()
    {
        DisplayName = null!;
    }

    public Guid UserId { get; private set; }
    public const int DisplayNameMaxLength = 60;
    public const int BioMaxLength = 160;

    public string DisplayName { get; private set; }
    public string? Bio { get; private set; }

    public AvatarKind AvatarKind { get; private set; }
    public Guid? AvatarGalleryItemId { get; private set; }
    public string? AvatarUrl { get; private set; }

    public ProfileVisibility Visibility { get; private set; }
    public DateTimeOffset? PublicProfileConsentedAt { get; private set; }
    public string? PublicProfileConsentVersion { get; private set; }

    /// <summary>When false, people who can open the profile do not see the biography.</summary>
    public bool ShowBio { get; private set; } = true;

    /// <summary>When false, people who can open the profile do not see named reports.</summary>
    public bool ShowPublicReports { get; private set; } = true;

    public bool ShowRealtimeLocation { get; private set; } = true;

    /// <summary>New reports and comments start anonymous. The person can switch a single post, or this default, to public.</summary>
    public bool PreferAnonymousReports { get; private set; } = true;

    public bool ShareLocationWithFriends { get; private set; }
    public LocationSharingMode FriendLocationSharingMode { get; private set; }
    public DateTimeOffset? FriendLocationSharingEndsAt { get; private set; }

    /// <summary>Temporary invisibility: hides the location from everybody without touching the other settings.</summary>
    public DateTimeOffset? GhostModeUntil { get; private set; }

    /// <summary>Denormalized location of the user's last report, shown when real-time location is off.</summary>
    public GeoPoint? LastReportLocation { get; private set; }

    public DateTimeOffset? LastReportAt { get; private set; }

    /// <summary>Every profile starts private, with the generic avatar.</summary>
    public static UserProfile CreateDefault(Guid userId, string displayName) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        DisplayName = Guard.NotEmpty(displayName, nameof(displayName), DisplayNameMaxLength),
        AvatarKind = AvatarKind.Default,
        Visibility = ProfileVisibility.Private,
        PreferAnonymousReports = true,
    };

    public void UpdateDetails(string displayName, string? bio)
    {
        DisplayName = Guard.NotEmpty(displayName, nameof(displayName), DisplayNameMaxLength);
        Bio = Guard.Optional(bio, nameof(bio), BioMaxLength);
    }

    public void UseDefaultAvatar()
    {
        AvatarKind = AvatarKind.Default;
        AvatarGalleryItemId = null;
        AvatarUrl = null;
    }

    public void UseGalleryAvatar(Guid galleryItemId, string imageUrl)
    {
        AvatarKind = AvatarKind.Gallery;
        AvatarGalleryItemId = Guard.NotEmpty(galleryItemId, nameof(galleryItemId));
        AvatarUrl = Guard.NotEmpty(imageUrl, nameof(imageUrl), 500);
    }

    public void UseCustomAvatar(string imageUrl)
    {
        AvatarKind = AvatarKind.Custom;
        AvatarGalleryItemId = null;
        AvatarUrl = Guard.NotEmpty(imageUrl, nameof(imageUrl), 500);
    }

    /// <summary>Turns the profile public after explicit consent. Not allowed for minors (RF-MIN-04).</summary>
    public void MakePublic(int age, DateTimeOffset consentedAt) => MakePublic(age, consentedAt, "unversioned");

    public void MakePublic(int age, DateTimeOffset consentedAt, string consentVersion)
    {
        Guard.That(
            MinorProtectionPolicy.CanHavePublicProfile(age),
            "profile.minor_cannot_be_public",
            "Users under 18 must keep a private profile.");

        Visibility = ProfileVisibility.Public;
        PublicProfileConsentedAt = consentedAt;
        PublicProfileConsentVersion = Guard.NotEmpty(consentVersion, nameof(consentVersion), 40);
        ShowRealtimeLocation = true;
    }

    public void SetPublicSwitches(bool showBio, bool showReports)
    {
        ShowBio = showBio;
        ShowPublicReports = showReports;
    }

    public void MakePrivate() => Visibility = ProfileVisibility.Private;

    public void SetPreferAnonymousReports(bool preferAnonymous) => PreferAnonymousReports = preferAnonymous;

    public void SetRealtimeLocationVisible(bool visible) => ShowRealtimeLocation = visible;

    public void EnableFriendLocationSharing(LocationSharingMode mode, DateTimeOffset? endsAt, DateTimeOffset now)
    {
        Guard.That(endsAt is null || endsAt > now, "profile.invalid_sharing_end", "The sharing end must be in the future.");

        ShareLocationWithFriends = true;
        FriendLocationSharingMode = mode;
        FriendLocationSharingEndsAt = endsAt;
    }

    public void DisableFriendLocationSharing()
    {
        ShareLocationWithFriends = false;
        FriendLocationSharingEndsAt = null;
    }

    /// <summary>Removes the public face of an account that has been deleted. The row stays.</summary>
    public void Erase(DateTimeOffset now)
    {
        DisplayName = "Cuenta eliminada";
        Bio = null;
        UseDefaultAvatar();
        MakePrivate();
        SetRealtimeLocationVisible(false);
        DisableFriendLocationSharing();
        ClearGhostMode();
        MarkUpdated(now);
    }

    public void ActivateGhostMode(DateTimeOffset until, DateTimeOffset now)
    {
        Guard.That(until > now, "profile.invalid_ghost_end", "Ghost mode must end in the future.");
        GhostModeUntil = until;
    }

    public void ClearGhostMode() => GhostModeUntil = null;

    public void RecordLastReport(GeoPoint location, DateTimeOffset now)
    {
        LastReportLocation = location;
        LastReportAt = now;
    }

    public bool IsGhost(DateTimeOffset now) => GhostModeUntil is { } until && until > now;

    public bool IsFriendSharingActive(DateTimeOffset now) =>
        ShareLocationWithFriends && (FriendLocationSharingEndsAt is null || FriendLocationSharingEndsAt > now);

    /// <summary>What anyone (strangers) sees on the public map. Private profiles never appear (RF-PER-04).</summary>
    public MapPresence PublicPresence(DateTimeOffset now)
    {
        if (Visibility != ProfileVisibility.Public || IsGhost(now))
        {
            return MapPresence.Hidden;
        }

        return ShowRealtimeLocation ? MapPresence.Realtime : MapPresence.LastReportOnly;
    }

    /// <summary>What accepted friends see: real time if the user shares with friends or is public in real time.</summary>
    public MapPresence FriendPresence(DateTimeOffset now)
    {
        if (IsGhost(now))
        {
            return MapPresence.Hidden;
        }

        if (IsFriendSharingActive(now))
        {
            return MapPresence.Realtime;
        }

        return PublicPresence(now);
    }
}
