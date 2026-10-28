using Situ.Domain.Common;
using Situ.Domain.Live;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Live;

public class LiveStreamScheduleTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void A_minor_cannot_schedule_a_stream_while_the_panel_setting_is_off()
    {
        var rejected = Assert.Throws<DomainException>(() => Schedule(age: 15, minorsCanStream: false, ContentVisibility.FriendsOnly));
        Assert.Equal("live.minors_cannot_stream", rejected.Code);
    }

    [Fact]
    public void An_adult_stream_waits_for_a_publisher_and_is_anonymous()
    {
        var stream = Schedule(age: 30, minorsCanStream: false, ContentVisibility.Public);

        Assert.Equal(LiveStreamStatus.Waiting, stream.Status);
        Assert.True(stream.IsAnonymous);
        Assert.True(stream.FacesBlurred);
        Assert.Null(stream.IngestKey);
        Assert.NotNull(stream.PublicPath);
    }

    [Fact]
    public void The_publish_key_works_once_and_then_the_stream_can_go_live()
    {
        var stream = Schedule(age: 30, minorsCanStream: false, ContentVisibility.FriendsOnly);
        var hash = stream.PublishKeyHash!;

        Assert.False(stream.AcceptPublishKey("other", Now));
        Assert.True(stream.AcceptPublishKey(hash, Now));
        Assert.False(stream.AcceptPublishKey(hash, Now));
        Assert.True(stream.MarkLive(Now.AddSeconds(2)));
        Assert.Equal(LiveStreamStatus.Live, stream.Status);
        Assert.False(stream.MarkLive(Now.AddSeconds(3)));
    }

    [Fact]
    public void An_expired_key_does_not_open_the_stream()
    {
        var stream = Schedule(age: 30, minorsCanStream: false, ContentVisibility.Public);
        Assert.False(stream.AcceptPublishKey(stream.PublishKeyHash!, Now.AddMinutes(3)));
    }

    private static LiveStream Schedule(int age, bool minorsCanStream, ContentVisibility visibility) =>
        LiveStream.Schedule(
            Guid.CreateVersion7(),
            age,
            minorsCanStream,
            null,
            null,
            null,
            "Retén en la 80",
            visibility,
            "abc123",
            "hash",
            Now.AddSeconds(120),
            Now);
}
