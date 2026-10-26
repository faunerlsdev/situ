using Situ.Api.Auth;
using Situ.Api.Configuration;

namespace Situ.Architecture.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void A_challenge_token_keeps_the_subject_and_the_scope()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tokens = new JwtTokenService(new JwtOptions
        {
            Issuer = "situ",
            Audience = "situ-clients",
            Secret = "test-secret-test-secret-test-secret-32",
        });

        var raw = tokens.CreateAccessToken(userId, now.AddMinutes(5), scope: "mfa_challenge");
        var ticket = tokens.ReadAccessToken(raw);

        Assert.NotNull(ticket);
        Assert.Equal(userId, ticket!.UserId);
        Assert.Equal("mfa_challenge", ticket.Scope);
        Assert.True(ticket.ExpiresAt > now);
    }
}
