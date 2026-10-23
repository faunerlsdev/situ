using Situ.Domain.Common;
using Situ.Domain.Profiles;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Profiles;

public class UserProfilePresenceTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void Public_and_friend_presence_follow_visibility_and_ghost_mode()
    {
        var later = Now.AddHours(2);
        var rows = new (string Name, Action<UserProfile> Arrange, DateTimeOffset At, MapPresence Public, MapPresence Friend)[]
        {
            ("private", _ => { }, Now, MapPresence.Hidden, MapPresence.Hidden),
            ("public realtime", profile => profile.MakePublic(18, Now), Now, MapPresence.Realtime, MapPresence.Realtime),
            (
                "public last report",
                profile =>
                {
                    profile.MakePublic(18, Now);
                    profile.SetRealtimeLocationVisible(false);
                },
                Now,
                MapPresence.LastReportOnly,
                MapPresence.LastReportOnly),
            (
                "friends see realtime while strangers see the last report",
                profile =>
                {
                    profile.MakePublic(18, Now);
                    profile.SetRealtimeLocationVisible(false);
                    profile.EnableFriendLocationSharing(LocationSharingMode.AllFriends, Now.AddHours(1), Now);
                },
                Now,
                MapPresence.LastReportOnly,
                MapPresence.Realtime),
            (
                "private profile shared with selected friends",
                profile => profile.EnableFriendLocationSharing(LocationSharingMode.SelectedFriends, null, Now),
                Now,
                MapPresence.Hidden,
                MapPresence.Realtime),
            (
                "friend sharing ended",
                profile =>
                {
                    profile.MakePublic(18, Now);
                    profile.SetRealtimeLocationVisible(false);
                    profile.EnableFriendLocationSharing(LocationSharingMode.AllFriends, Now.AddHours(1), Now);
                },
                later,
                MapPresence.LastReportOnly,
                MapPresence.LastReportOnly),
            (
                "ghost mode hides everyone",
                profile =>
                {
                    profile.MakePublic(18, Now);
                    profile.EnableFriendLocationSharing(LocationSharingMode.AllFriends, null, Now);
                    profile.ActivateGhostMode(Now.AddHours(1), Now);
                },
                Now,
                MapPresence.Hidden,
                MapPresence.Hidden),
            (
                "ghost mode ended",
                profile =>
                {
                    profile.MakePublic(18, Now);
                    profile.ActivateGhostMode(Now.AddHours(1), Now);
                },
                later,
                MapPresence.Realtime,
                MapPresence.Realtime),
        };

        foreach (var row in rows)
        {
            var profile = UserProfile.CreateDefault(Guid.CreateVersion7(), "Ana");
            row.Arrange(profile);

            Assert.True(profile.PublicPresence(row.At) == row.Public, $"{row.Name} public presence");
            Assert.True(profile.FriendPresence(row.At) == row.Friend, $"{row.Name} friend presence");
        }
    }

    [Fact]
    public void Minors_stay_off_the_public_map()
    {
        var profile = UserProfile.CreateDefault(Guid.CreateVersion7(), "Ana");

        var rejected = Assert.Throws<DomainException>(() => profile.MakePublic(17, Now));

        Assert.Equal("profile.minor_cannot_be_public", rejected.Code);
        Assert.Equal(ProfileVisibility.Private, profile.Visibility);
        Assert.Equal(MapPresence.Hidden, profile.PublicPresence(Now));

        profile.EnableFriendLocationSharing(LocationSharingMode.AllFriends, null, Now);

        Assert.Equal(MapPresence.Hidden, profile.PublicPresence(Now));
        Assert.Equal(MapPresence.Realtime, profile.FriendPresence(Now));

        profile.ActivateGhostMode(Now.AddMinutes(30), Now);
        Assert.Equal(MapPresence.Hidden, profile.FriendPresence(Now));
    }
}
