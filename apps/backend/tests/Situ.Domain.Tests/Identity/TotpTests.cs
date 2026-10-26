using System.Text;
using Situ.Domain.Identity;

namespace Situ.Domain.Tests.Identity;

public class TotpTests
{
    [Fact]
    public void Sha1_matches_the_rfc6238_vector()
    {
        var secret = Totp.Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        var at = DateTimeOffset.FromUnixTimeSeconds(59);

        Assert.Equal("287082", Totp.Code(secret, at));
        Assert.True(Totp.Check(secret, "287082", at, null).Accepted);
    }

    [Fact]
    public void A_code_inside_the_previous_window_is_accepted_once()
    {
        var secret = Totp.GenerateSecret();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var previous = Totp.Code(secret, now.AddSeconds(-30));
        var first = Totp.Check(secret, previous, now, null);
        var second = Totp.Check(secret, previous, now, first.Step);

        Assert.True(first.Accepted);
        Assert.True(second.Reused);
        Assert.False(second.Accepted);
    }
}
