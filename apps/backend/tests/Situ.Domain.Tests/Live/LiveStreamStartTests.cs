using Situ.Domain.Common;
using Situ.Domain.Live;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Live;

public class LiveStreamStartTests
{
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly GeoPoint Location = new(4.7110, -74.0721);

    [Fact]
    public void Minor_can_only_stream_to_friends_and_is_always_anonymous()
    {
        var rejected = Assert.Throws<DomainException>(() => Start(age: 15, ContentVisibility.Public, requestedAnonymous: false));
        Assert.Equal("live.minor_friends_only", rejected.Code);

        var stream = Start(age: 15, ContentVisibility.FriendsOnly, requestedAnonymous: false);

        Assert.Equal(ContentVisibility.FriendsOnly, stream.Visibility);
        Assert.True(stream.StreamerIsMinor);
        Assert.True(stream.IsAnonymous);
        Assert.True(stream.FacesBlurred);
        Assert.True(stream.VoiceModified);
        Assert.Equal(LiveStreamStatus.Live, stream.Status);
    }

    [Fact]
    public void Adult_anonymity_blurs_faces_and_modifies_voice()
    {
        var anonymous = Start(age: 30, ContentVisibility.Public, requestedAnonymous: true);

        Assert.Equal(ContentVisibility.Public, anonymous.Visibility);
        Assert.False(anonymous.StreamerIsMinor);
        Assert.True(anonymous.IsAnonymous);
        Assert.True(anonymous.FacesBlurred);
        Assert.True(anonymous.VoiceModified);

        var named = Start(age: 30, ContentVisibility.Public, requestedAnonymous: false);

        Assert.False(named.IsAnonymous);
        Assert.False(named.FacesBlurred);
        Assert.False(named.VoiceModified);

        var friends = Start(age: 30, ContentVisibility.FriendsOnly, requestedAnonymous: false);

        Assert.Equal(ContentVisibility.FriendsOnly, friends.Visibility);
        Assert.False(friends.IsAnonymous);
        Assert.False(friends.FacesBlurred);
        Assert.False(friends.VoiceModified);
    }

    private static LiveStream Start(int age, ContentVisibility visibility, bool requestedAnonymous) =>
        LiveStream.Start(
            Guid.CreateVersion7(),
            age,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Location,
            "Retén en la 80",
            visibility,
            requestedAnonymous,
            "ingest-key",
            Now);
}
